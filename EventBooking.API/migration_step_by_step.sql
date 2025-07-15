-- Step by step migration script for removing Sections and using TicketTypes

-- Step 1: Add required columns to TicketTypes if they don't exist
PRINT 'Step 1: Adding required columns to TicketTypes';
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NULL;
    PRINT '- Added Name column to TicketTypes';
END
ELSE
BEGIN
    PRINT '- Name column already exists in TicketTypes';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
BEGIN
    ALTER TABLE TicketTypes ADD Color nvarchar(7) NULL;
    PRINT '- Added Color column to TicketTypes';
END
ELSE
BEGIN
    PRINT '- Color column already exists in TicketTypes';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Description')
BEGIN
    ALTER TABLE TicketTypes ADD Description nvarchar(500) NULL;
    PRINT '- Added Description column to TicketTypes';
END
ELSE
BEGIN
    PRINT '- Description column already exists in TicketTypes';
END

-- Step 2: Add TicketTypeId to Seats table if it doesn't exist
PRINT 'Step 2: Adding TicketTypeId column to Seats table';
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    ALTER TABLE Seats ADD TicketTypeId int NULL;
    PRINT '- Added TicketTypeId column to Seats table';
END
ELSE
BEGIN
    PRINT '- TicketTypeId column already exists in Seats table';
END

-- Step 3: Set initial values for new columns in TicketTypes
PRINT 'Step 3: Setting initial values for new columns in TicketTypes';
UPDATE TicketTypes 
SET 
    Name = ISNULL(Name, Type),
    Color = ISNULL(Color, '#007bff'),
    Description = ISNULL(Description, Type)
WHERE Name IS NULL OR Color IS NULL OR Description IS NULL;
PRINT '- Updated TicketTypes with initial values';

-- Step 4: If Sections table exists, create TicketTypes based on Sections
PRINT 'Step 4: Creating TicketTypes based on Sections';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    PRINT '- Sections table exists, creating TicketTypes based on Sections';
    
    -- Create TicketTypes for each Event based on Sections
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
    PRINT '- Created TicketTypes based on Sections';
END
ELSE
BEGIN
    PRINT '- Sections table does not exist, skipping section data migration';
END

-- Step 5: Update Seats to reference TicketTypes
PRINT 'Step 5: Updating Seats to reference TicketTypes';
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    -- Assign a default TicketType to each Seat in each Event
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
    
    PRINT '- Updated Seats with TicketTypeId values';
END
ELSE
BEGIN
    PRINT '- TicketTypeId column does not exist in Seats table, skipping update';
END

-- Step 6: Check for and drop foreign key constraints referencing SectionId
PRINT 'Step 6: Removing SectionId foreign key constraints';
DECLARE @constraintSql NVARCHAR(MAX) = '';
SELECT @constraintSql = @constraintSql + 'ALTER TABLE ' + QUOTENAME(fk.TABLE_SCHEMA) + '.' + QUOTENAME(fk.TABLE_NAME) + 
                       ' DROP CONSTRAINT ' + QUOTENAME(fk.CONSTRAINT_NAME) + ';' + CHAR(13)
FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS fk
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON fk.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
WHERE ku.COLUMN_NAME = 'SectionId';

IF @constraintSql != ''
BEGIN
    EXEC sp_executesql @constraintSql;
    PRINT '- Dropped foreign key constraints for SectionId';
END
ELSE
BEGIN
    PRINT '- No foreign key constraints found for SectionId';
END

-- Step 7: Drop SectionId columns if they exist
PRINT 'Step 7: Removing SectionId columns';
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
BEGIN
    ALTER TABLE Seats DROP COLUMN SectionId;
    PRINT '- Dropped SectionId column from Seats table';
END
ELSE
BEGIN
    PRINT '- SectionId column does not exist in Seats table';
END

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
BEGIN
    ALTER TABLE Tables DROP COLUMN SectionId;
    PRINT '- Dropped SectionId column from Tables table';
END
ELSE
BEGIN
    PRINT '- SectionId column does not exist in Tables table';
END

-- Step 8: Make TicketTypeId NOT NULL for Seats
PRINT 'Step 8: Making TicketTypeId NOT NULL for Seats';
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId' AND IS_NULLABLE = 'YES')
BEGIN
    -- Update any remaining NULL values with a default value
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
    ALTER TABLE Seats ALTER COLUMN TicketTypeId int NOT NULL;
    PRINT '- Made TicketTypeId column NOT NULL in Seats table';
END
ELSE
BEGIN
    PRINT '- TicketTypeId column already set to NOT NULL in Seats table';
END

-- Step 9: Create foreign key constraint for Seats -> TicketTypes
PRINT 'Step 9: Creating foreign key constraint for Seats -> TicketTypes';
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_Seats_TicketTypes_TicketTypeId')
BEGIN
    ALTER TABLE Seats 
    ADD CONSTRAINT FK_Seats_TicketTypes_TicketTypeId 
    FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id);
    PRINT '- Created foreign key constraint for Seats -> TicketTypes';
END
ELSE
BEGIN
    PRINT '- Foreign key constraint for Seats -> TicketTypes already exists';
END

-- Step 10: Create index for performance
PRINT 'Step 10: Creating index for TicketTypeId';
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_TicketTypeId' AND object_id = OBJECT_ID('Seats'))
BEGIN
    CREATE INDEX IX_Seats_TicketTypeId ON Seats(TicketTypeId);
    PRINT '- Created index IX_Seats_TicketTypeId';
END
ELSE
BEGIN
    PRINT '- Index IX_Seats_TicketTypeId already exists';
END

-- Step 11: Drop Sections table if all references have been removed
PRINT 'Step 11: Dropping Sections table if no references exist';
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
AND NOT EXISTS (
    SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS fk
    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON fk.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
    WHERE ku.REFERENCED_TABLE_NAME = 'Sections'
)
BEGIN
    DROP TABLE Sections;
    PRINT '- Dropped Sections table';
END
ELSE
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
        PRINT '- Sections table does not exist';
    ELSE
        PRINT '- Sections table still has references, cannot drop it yet';
END

PRINT 'Migration completed';
