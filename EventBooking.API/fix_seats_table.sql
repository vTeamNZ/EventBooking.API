-- Script to fix Seats table

-- Add the TicketTypeId column to the Seats table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    PRINT 'Adding TicketTypeId column to Seats table';
    ALTER TABLE Seats ADD TicketTypeId int NULL;
END
ELSE
BEGIN
    PRINT 'TicketTypeId column already exists in Seats table';
END
