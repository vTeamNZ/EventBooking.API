-- SQL script to update seat status field
-- Run this script on the database after deployment

-- First check if Status column exists, add it if it doesn't
IF NOT EXISTS (
    SELECT 1 
    FROM sys.columns 
    WHERE Name = N'Status'
    AND Object_ID = OBJECT_ID(N'Seats')
)
BEGIN
    -- Add Status column with default value of 0 (Available)
    ALTER TABLE Seats ADD Status int NOT NULL DEFAULT(0);
    PRINT 'Added Status column to Seats table';
END
ELSE
BEGIN
    PRINT 'Status column already exists in Seats table';
END

-- Update Status based on IsReserved flag for backward compatibility
UPDATE Seats SET Status = 1 WHERE IsReserved = 1 AND Status = 0;
PRINT 'Updated seat status based on IsReserved flag';

-- Seats with ReservedUntil in the future should be marked as Reserved
UPDATE Seats SET Status = 1 
WHERE ReservedUntil IS NOT NULL 
AND ReservedUntil > GETUTCDATE() 
AND Status = 0;
PRINT 'Updated seat status based on ReservedUntil date';

PRINT 'Seat status update completed';
