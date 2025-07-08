-- Simple Section to TicketType Migration Script
-- First add the new columns to TicketTypes table

-- 1. Add required columns to TicketTypes if they don't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NOT NULL DEFAULT ''
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
BEGIN
    ALTER TABLE TicketTypes ADD Color nvarchar(7) NOT NULL DEFAULT '#007bff'
END

-- 2. Ensure TicketTypeId exists in Seats table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    ALTER TABLE Seats ADD TicketTypeId int NULL
    PRINT 'Added TicketTypeId column to Seats table'
END
ELSE
BEGIN
    PRINT 'TicketTypeId column already exists in Seats table'
END

-- 3. Set initial values for new columns
UPDATE TicketTypes 
SET Name = Type 
WHERE Name = '' OR Name IS NULL;

-- 4. If Sections table exists, copy data to TicketTypes
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    -- Create TicketTypes based on Sections
    INSERT INTO TicketTypes (Type, Name, Color, Price, EventId, Description)
    SELECT DISTINCT 
        s.Name,
        s.Name,
        s.Color,
        s.BasePrice,
        e.Id,
        'Migrated from Section: ' + s.Name
    FROM Sections s
    CROSS JOIN Events e
    WHERE NOT EXISTS (
        SELECT 1 FROM TicketTypes tt 
        WHERE tt.EventId = e.Id AND tt.Name = s.Name
    );

    -- Update Seats to reference TicketTypes instead of Sections
    -- First, make sure the TicketTypeId column exists before updating it
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
    BEGIN
        PRINT 'Updating Seats with TicketTypeId values'

        -- For each event, update seats to reference appropriate TicketType 
        -- (first available TicketType for the event)
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
    END

    -- Drop Section foreign keys and columns if they exist
    DECLARE @sql NVARCHAR(MAX) = '';
    SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(fk.TABLE_SCHEMA) + '.' + QUOTENAME(fk.TABLE_NAME) + ' DROP CONSTRAINT ' + QUOTENAME(fk.CONSTRAINT_NAME) + ';' + CHAR(13)
    FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS fk
    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON fk.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
    WHERE ku.COLUMN_NAME = 'SectionId';
    
    IF @sql != ''
        EXEC sp_executesql @sql;

    -- Drop SectionId column from Seats if it exists
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
    BEGIN
        ALTER TABLE Seats DROP COLUMN SectionId
    END

    -- Drop SectionId column from Tables if it exists
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
    BEGIN
        ALTER TABLE Tables DROP COLUMN SectionId
    END

    -- Drop Sections table if all references have been removed
    IF NOT EXISTS (
        SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS fk
        JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON fk.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
        WHERE ku.REFERENCED_TABLE_NAME = 'Sections'
    )
    BEGIN
        DROP TABLE Sections;
    END
END

-- 5. Make TicketTypeId NOT NULL for Seats after migration (if needed)
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId' AND IS_NULLABLE = 'YES')
BEGIN
    -- Update any remaining NULL values
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

    -- Make column NOT NULL
    ALTER TABLE Seats ALTER COLUMN TicketTypeId int NOT NULL
END

-- 6. Create foreign key constraint for Seats -> TicketTypes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_Seats_TicketTypes_TicketTypeId')
BEGIN
    ALTER TABLE Seats 
    ADD CONSTRAINT FK_Seats_TicketTypes_TicketTypeId 
    FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id)
END

-- 7. Create index for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_TicketTypeId')
BEGIN
    CREATE INDEX IX_Seats_TicketTypeId ON Seats(TicketTypeId)
END
