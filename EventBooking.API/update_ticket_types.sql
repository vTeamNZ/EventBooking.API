-- Now update the TicketTypes table

-- Add Name column to TicketTypes if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    PRINT 'Adding Name column to TicketTypes';
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NULL;
END
ELSE
BEGIN
    PRINT 'Name column already exists in TicketTypes';
END

-- Add Color column to TicketTypes if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
BEGIN
    PRINT 'Adding Color column to TicketTypes';
    ALTER TABLE TicketTypes ADD Color nvarchar(7) NULL;
END
ELSE
BEGIN
    PRINT 'Color column already exists in TicketTypes';
END

-- Set initial values for new columns in TicketTypes
PRINT 'Setting initial values for new columns in TicketTypes';
UPDATE TicketTypes 
SET 
    Name = CASE WHEN Name IS NULL THEN Type ELSE Name END,
    Color = CASE WHEN Color IS NULL THEN '#007bff' ELSE Color END
WHERE Name IS NULL OR Color IS NULL;

-- Update Seats with TicketTypeId values
PRINT 'Updating Seats with TicketTypeId values';
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

-- Count seats that still have NULL TicketTypeId
SELECT COUNT(*) AS SeatsWithNullTicketTypeId FROM Seats WHERE TicketTypeId IS NULL;
