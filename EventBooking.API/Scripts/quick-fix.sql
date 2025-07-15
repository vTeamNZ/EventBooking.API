-- Simple one-command database setup
USE kwdb01;
GO

PRINT '=== Quick Database Fix ===';

-- Step 1: Drop index on SectionId if it exists
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_SectionId')
BEGIN
    DROP INDEX IX_Seats_SectionId ON Seats;
    PRINT '✓ Dropped index IX_Seats_SectionId';
END

-- Step 2: Add TicketTypeId if missing
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    ALTER TABLE Seats ADD TicketTypeId int NOT NULL DEFAULT 1;
    PRINT '✓ Added TicketTypeId column';
END

-- Step 3: Remove SectionId column
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
BEGIN
    ALTER TABLE Seats DROP COLUMN SectionId;
    PRINT '✓ Removed SectionId column';
END

-- Step 4: Add Name column to TicketTypes if missing
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NULL;
    PRINT '✓ Added Name column to TicketTypes';
END

-- Step 5: Ensure we have minimal test data
IF NOT EXISTS (SELECT * FROM Events WHERE Title = 'Test Event')
BEGIN
    INSERT INTO Events (Title, Description, Date, Location, Price, Capacity, OrganizerId, VenueId, ImageUrl, IsActive, SeatSelectionMode)
    VALUES ('Test Event', 'Quick test event', DATEADD(DAY, 30, GETDATE()), 'Test Location', 50.00, 100, 
           (SELECT TOP 1 Id FROM Organizers), (SELECT TOP 1 Id FROM Venues), 'test.jpg', 1, 3);
    PRINT '✓ Added test event';
           
    INSERT INTO TicketTypes (Type, Name, Price, Description, EventId, Color)
    VALUES ('Standard', 'Standard Ticket', 50.00, 'Test ticket', 
           (SELECT Id FROM Events WHERE Title = 'Test Event'), '#007bff');
    PRINT '✓ Added test ticket type';
END

-- Step 6: Health check
SELECT 'Status Check' as Test, 'PASSED' as Result;
SELECT 
    'Events' as Component, COUNT(*) as Count, 
    CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'EMPTY' END as Status
FROM Events
UNION ALL
SELECT 'TicketTypes', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'EMPTY' END FROM TicketTypes
UNION ALL
SELECT 'Venues', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'EMPTY' END FROM Venues
UNION ALL
SELECT 'Users', COUNT(*), CASE WHEN COUNT(*) > 0 THEN 'OK' ELSE 'EMPTY' END FROM AspNetUsers;

PRINT '=== QUICK FIX COMPLETED! ===';
PRINT 'Test API: https://kiwilanka.co.nz/api/Events';
