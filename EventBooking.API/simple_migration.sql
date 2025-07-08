-- Focused migration script
PRINT 'Step 1: Adding necessary columns to TicketTypes if they do not exist';

-- Add Name column to TicketTypes if it doesn't exist
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

-- Add Color column to TicketTypes if it doesn't exist
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

-- Add TicketTypeId to Seats table if it doesn't exist
PRINT 'Step 2: Adding TicketTypeId column to Seats table if it does not exist';
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

-- Set initial values for new columns in TicketTypes
PRINT 'Step 3: Setting initial values for new columns in TicketTypes';
UPDATE TicketTypes 
SET 
    Name = CASE WHEN Name IS NULL THEN Type ELSE Name END,
    Color = CASE WHEN Color IS NULL THEN '#007bff' ELSE Color END
WHERE Name IS NULL OR Color IS NULL;
PRINT '- Updated TicketTypes with initial values';

-- Now update the Seats with proper TicketTypeId values
PRINT 'Step 4: Updating Seats with TicketTypeId values';
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
PRINT '- Updated Seats with default TicketTypeId values';

-- Once all seats have a TicketTypeId, make it NOT NULL (if there are no NULL values)
PRINT 'Step 5: Checking if we can make TicketTypeId NOT NULL';
IF NOT EXISTS (SELECT 1 FROM Seats WHERE TicketTypeId IS NULL)
BEGIN
    ALTER TABLE Seats ALTER COLUMN TicketTypeId int NOT NULL;
    PRINT '- Made TicketTypeId column NOT NULL in Seats table';
END
ELSE
BEGIN
    PRINT '- Cannot make TicketTypeId NOT NULL as there are still NULL values';
    SELECT COUNT(*) AS SeatsWithNullTicketTypeId FROM Seats WHERE TicketTypeId IS NULL;
END

-- Create foreign key constraint for Seats -> TicketTypes if it doesn't exist
PRINT 'Step 6: Creating foreign key constraint for Seats -> TicketTypes if needed';
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_Seats_TicketTypes_TicketTypeId')
AND NOT EXISTS (SELECT 1 FROM Seats WHERE TicketTypeId IS NULL)
BEGIN
    ALTER TABLE Seats 
    ADD CONSTRAINT FK_Seats_TicketTypes_TicketTypeId 
    FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id);
    PRINT '- Created foreign key constraint for Seats -> TicketTypes';
END
ELSE
BEGIN
    PRINT '- Foreign key constraint not created - either it already exists or there are NULL values';
END

PRINT 'Migration steps completed';
