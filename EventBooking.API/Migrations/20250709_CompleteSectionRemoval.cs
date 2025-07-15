using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventBooking.API.Migrations
{
    /// <inheritdoc />
    public partial class CompleteSectionRemoval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add required columns to TicketTypes if they don't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
                BEGIN
                    ALTER TABLE TicketTypes ADD Name nvarchar(100) NOT NULL DEFAULT ''
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
                BEGIN
                    ALTER TABLE TicketTypes ADD Color nvarchar(7) NOT NULL DEFAULT '#007bff'
                END
            ");

            // 2. Ensure TicketTypeId exists in Seats table
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
                BEGIN
                    ALTER TABLE Seats ADD TicketTypeId int NULL
                END
            ");

            // 3. Copy Section data to TicketTypes and migrate relationships
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
                BEGIN
                    -- First, populate Name from Type if Name is empty
                    UPDATE TicketTypes 
                    SET Name = Type 
                    WHERE Name = '' OR Name IS NULL;

                    -- Update existing TicketTypes with Section data where there's a match
                    UPDATE tt
                    SET tt.Name = CASE 
                            WHEN s.Name IS NOT NULL AND s.Name != '' THEN s.Name 
                            ELSE tt.Type 
                        END,
                        tt.Color = COALESCE(s.Color, tt.Color),
                        tt.Price = COALESCE(s.BasePrice, tt.Price)
                    FROM TicketTypes tt
                    LEFT JOIN Sections s ON tt.Type = s.Name;

                    -- Create new TicketTypes for Sections that don't have corresponding TicketTypes
                    INSERT INTO TicketTypes (Type, Name, Color, Price, EventId, Description)
                    SELECT DISTINCT 
                        s.Name,
                        s.Name,
                        COALESCE(s.Color, '#007bff'),
                        COALESCE(s.BasePrice, 0),
                        e.Id as EventId,
                        'Migrated from Section: ' + s.Name
                    FROM Sections s
                    CROSS JOIN Events e
                    WHERE NOT EXISTS (
                        SELECT 1 FROM TicketTypes tt 
                        WHERE tt.EventId = e.Id AND (tt.Name = s.Name OR tt.Type = s.Name)
                    )
                    AND EXISTS (
                        SELECT 1 FROM Seats seat 
                        WHERE seat.SectionId = s.Id AND seat.EventId = e.Id
                    );

                    -- Update seats to reference TicketTypes
                    UPDATE seat
                    SET seat.TicketTypeId = tt.Id
                    FROM Seats seat
                    INNER JOIN Sections s ON seat.SectionId = s.Id
                    INNER JOIN TicketTypes tt ON (tt.Name = s.Name OR tt.Type = s.Name) AND tt.EventId = seat.EventId
                    WHERE seat.SectionId IS NOT NULL AND seat.TicketTypeId IS NULL;

                    -- Update tables to reference TicketTypes if Tables table exists
                    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Tables')
                    BEGIN
                        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                                   WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
                        BEGIN
                            -- Add TicketTypeId to Tables if it doesn't exist
                            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                                           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'TicketTypeId')
                            BEGIN
                                ALTER TABLE Tables ADD TicketTypeId int NULL
                            END

                            -- Update tables to reference TicketTypes
                            UPDATE t
                            SET t.TicketTypeId = tt.Id
                            FROM Tables t
                            INNER JOIN Sections s ON t.SectionId = s.Id
                            INNER JOIN TicketTypes tt ON (tt.Name = s.Name OR tt.Type = s.Name) AND tt.EventId = t.EventId
                            WHERE t.SectionId IS NOT NULL AND t.TicketTypeId IS NULL;
                        END
                    END

                    -- Drop foreign key constraints
                    DECLARE @sql NVARCHAR(MAX) = '';
                    SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(fk.TABLE_SCHEMA) + '.' + QUOTENAME(fk.TABLE_NAME) + ' DROP CONSTRAINT ' + QUOTENAME(fk.CONSTRAINT_NAME) + ';' + CHAR(13)
                    FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS fk
                    WHERE fk.REFERENCED_TABLE_NAME = 'Sections';
                    
                    IF @sql != ''
                        EXEC sp_executesql @sql;

                    -- Drop SectionId columns
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
                    BEGIN
                        ALTER TABLE Seats DROP COLUMN SectionId
                    END

                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                               WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
                    BEGIN
                        ALTER TABLE Tables DROP COLUMN SectionId
                    END

                    -- Drop Sections table
                    DROP TABLE Sections;
                END
            ");

            // 4. Update any seats without TicketTypeId to reference first available TicketType
            migrationBuilder.Sql(@"
                UPDATE s 
                SET s.TicketTypeId = (
                    SELECT TOP 1 tt.Id 
                    FROM TicketTypes tt 
                    WHERE tt.EventId = s.EventId
                    ORDER BY tt.Id
                )
                FROM Seats s
                WHERE s.TicketTypeId IS NULL
                AND EXISTS (SELECT 1 FROM TicketTypes tt WHERE tt.EventId = s.EventId);
            ");

            // 5. Make TicketTypeId NOT NULL after data migration
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId' AND IS_NULLABLE = 'YES')
                BEGIN
                    ALTER TABLE Seats ALTER COLUMN TicketTypeId int NOT NULL
                END
            ");

            // 6. Create foreign key constraint for Seats -> TicketTypes
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
                               WHERE CONSTRAINT_NAME = 'FK_Seats_TicketTypes_TicketTypeId')
                BEGIN
                    ALTER TABLE Seats 
                    ADD CONSTRAINT FK_Seats_TicketTypes_TicketTypeId 
                    FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id)
                END
            ");

            // 7. Create foreign key constraint for Tables -> TicketTypes if Tables table exists
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Tables')
                AND EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'TicketTypeId')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
                                   WHERE CONSTRAINT_NAME = 'FK_Tables_TicketTypes_TicketTypeId')
                    BEGIN
                        -- Make TicketTypeId NOT NULL for Tables if it has data
                        UPDATE t 
                        SET t.TicketTypeId = (
                            SELECT TOP 1 tt.Id 
                            FROM TicketTypes tt 
                            WHERE tt.EventId = t.EventId
                            ORDER BY tt.Id
                        )
                        FROM Tables t
                        WHERE t.TicketTypeId IS NULL
                        AND EXISTS (SELECT 1 FROM TicketTypes tt WHERE tt.EventId = t.EventId);

                        ALTER TABLE Tables ALTER COLUMN TicketTypeId int NOT NULL;
                        
                        ALTER TABLE Tables 
                        ADD CONSTRAINT FK_Tables_TicketTypes_TicketTypeId 
                        FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id)
                    END
                END
            ");

            // 8. Create indexes for performance
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_TicketTypeId')
                BEGIN
                    CREATE INDEX IX_Seats_TicketTypeId ON Seats(TicketTypeId)
                END

                IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Tables')
                AND EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'TicketTypeId')
                BEGIN
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tables_TicketTypeId')
                    BEGIN
                        CREATE INDEX IX_Tables_TicketTypeId ON Tables(TicketTypeId)
                    END
                END
            ");

            // 9. Ensure all TicketTypes have proper names (fallback to Type)
            migrationBuilder.Sql(@"
                UPDATE TicketTypes 
                SET Name = Type 
                WHERE Name = '' OR Name IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This is a destructive migration - down migration would be complex
            // and is not recommended for production use
            throw new NotSupportedException("This migration cannot be reversed safely due to data loss.");
        }
    }
}
