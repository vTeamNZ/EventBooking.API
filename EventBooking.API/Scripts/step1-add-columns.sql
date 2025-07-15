-- Step 1: Just add the missing columns first
USE kwdb01;
GO

PRINT 'Adding missing columns...';

-- Add Name column to TicketTypes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NULL;
    PRINT 'Added Name column to TicketTypes table';
END
ELSE
BEGIN
    PRINT 'Name column already exists in TicketTypes table';
END

-- Add Color column to TicketTypes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
BEGIN
    ALTER TABLE TicketTypes ADD Color nvarchar(7) NOT NULL DEFAULT '#007bff';
    PRINT 'Added Color column to TicketTypes table';
END
ELSE
BEGIN
    PRINT 'Color column already exists in TicketTypes table';
END

-- Add SeatRowAssignments column to TicketTypes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'SeatRowAssignments')
BEGIN
    ALTER TABLE TicketTypes ADD SeatRowAssignments nvarchar(max) NULL;
    PRINT 'Added SeatRowAssignments column to TicketTypes table';
END
ELSE
BEGIN
    PRINT 'SeatRowAssignments column already exists in TicketTypes table';
END

-- Add TicketTypeId column to Seats
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    ALTER TABLE Seats ADD TicketTypeId int NOT NULL DEFAULT 1;
    PRINT 'Added TicketTypeId column to Seats table';
END
ELSE
BEGIN
    PRINT 'TicketTypeId column already exists in Seats table';
END

PRINT 'Column additions completed!';

-- Show current schema
PRINT 'Current TicketTypes columns:';
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' ORDER BY ORDINAL_POSITION;

PRINT 'Current Seats columns:';
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' ORDER BY ORDINAL_POSITION;
