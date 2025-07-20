-- MANUAL MIGRATION SCRIPT FOR EVENT ID 4 "LADIES NIGHT"
-- Step 1: Run this script on kwdb01 database to get the data
-- Step 2: Copy the generated INSERT statements
-- Step 3: Run those statements on kwdb02 database

USE kwdb01;

-- ==== STEP 1: GET EVENT DETAILS ====
SELECT 'Event Details:' as Info;
SELECT Id, Title, Description, Date, Location, Price, Capacity, OrganizerId, VenueId, IsActive, SeatSelectionMode
FROM Events WHERE Id = 4;

-- ==== STEP 2: GENERATE INSERT STATEMENTS ====

-- Events
SELECT 'INSERT INTO Events (Id, Title, Description, Date, Location, Price, Capacity, OrganizerId, ImageUrl, IsActive, SeatSelectionMode, StagePosition, VenueId, ProcessingFeePercentage, ProcessingFeeFixedAmount) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ''' + REPLACE(Title, '''', '''''') + ''', ''' + REPLACE(ISNULL(Description, ''), '''', '''''') + ''', ''' + 
    CONVERT(NVARCHAR, Date, 120) + ''', ''' + REPLACE(Location, '''', '''''') + ''', ' + 
    ISNULL(CAST(Price AS NVARCHAR), 'NULL') + ', ' + ISNULL(CAST(Capacity AS NVARCHAR), 'NULL') + ', ' + 
    ISNULL(CAST(OrganizerId AS NVARCHAR), 'NULL') + ', ''' + ISNULL(ImageUrl, '') + ''', ' + 
    CAST(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ' + CAST(SeatSelectionMode AS NVARCHAR) + ', ''' + 
    ISNULL(StagePosition, '') + ''', ' + ISNULL(CAST(VenueId AS NVARCHAR), 'NULL') + ', ' +
    ISNULL(CAST(ProcessingFeePercentage AS NVARCHAR), '0') + ', ' + ISNULL(CAST(ProcessingFeeFixedAmount AS NVARCHAR), '0') + ');' as EventInsert
FROM Events WHERE Id = 4;

-- TicketTypes
SELECT 'INSERT INTO TicketTypes (Id, Type, Price, Description, EventId, SeatRowAssignments, Color, Name) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ''' + REPLACE(Type, '''', '''''') + ''', ' + CAST(Price AS NVARCHAR) + ', ''' + 
    ISNULL(REPLACE(Description, '''', ''''''), '') + ''', ' + CAST(EventId AS NVARCHAR) + ', ''' + 
    ISNULL(SeatRowAssignments, '') + ''', ''' + Color + ''', ''' + ISNULL(Name, '') + ''');' as TicketTypeInsert
FROM TicketTypes WHERE EventId = 4;

-- FoodItems
SELECT 'INSERT INTO FoodItems (Id, Name, Price, Description, EventId) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ''' + REPLACE(Name, '''', '''''') + ''', ' + CAST(Price AS NVARCHAR) + ', ''' + 
    ISNULL(REPLACE(Description, '''', ''''''), '') + ''', ' + CAST(EventId AS NVARCHAR) + ');' as FoodItemInsert
FROM FoodItems WHERE EventId = 4;

-- Seats (showing first 10, you'll need to get all)
SELECT TOP 10 'INSERT INTO Seats (Id, EventId, Row, Number, IsReserved, Height, Price, ReservedBy, ReservedUntil, SeatNumber, Status, TableId, Width, X, Y, TicketTypeId) VALUES (' +
    CAST(Id AS NVARCHAR) + ', ' + CAST(EventId AS NVARCHAR) + ', ''' + Row + ''', ' + CAST(Number AS NVARCHAR) + ', ' + 
    CAST(CASE WHEN IsReserved = 1 THEN 1 ELSE 0 END AS NVARCHAR) + ', ' + CAST(Height AS NVARCHAR) + ', ' + CAST(Price AS NVARCHAR) + ', ''' + 
    ISNULL(ReservedBy, '') + ''', ' + CASE WHEN ReservedUntil IS NULL THEN 'NULL' ELSE '''' + CONVERT(NVARCHAR, ReservedUntil, 120) + '''' END + ', ''' + 
    SeatNumber + ''', ' + CAST(Status AS NVARCHAR) + ', ' + ISNULL(CAST(TableId AS NVARCHAR), 'NULL') + ', ' + 
    CAST(Width AS NVARCHAR) + ', ' + CAST(X AS NVARCHAR) + ', ' + CAST(Y AS NVARCHAR) + ', ' + CAST(TicketTypeId AS NVARCHAR) + ');' as SeatInsert
FROM Seats WHERE EventId = 4;

-- Show counts
SELECT 'RECORD COUNTS:' as Info;
SELECT 'Events' as TableName, COUNT(*) as Count FROM Events WHERE Id = 4
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes WHERE EventId = 4
UNION ALL SELECT 'FoodItems', COUNT(*) FROM FoodItems WHERE EventId = 4
UNION ALL SELECT 'Seats', COUNT(*) FROM Seats WHERE EventId = 4
UNION ALL SELECT 'Tables', COUNT(*) FROM Tables WHERE EventId = 4;

-- ==== STEP 3: BEFORE IMPORTING TO kwdb02, RUN THIS CLEANUP ====
/*
USE kwdb02;

-- Clean up any existing Event ID 4 data
DELETE FROM SeatReservations WHERE EventId = 4;
DELETE FROM Reservations WHERE EventId = 4;
DELETE FROM BookingFoods WHERE BookingId IN (SELECT Id FROM Bookings WHERE EventId = 4);
DELETE FROM BookingTickets WHERE BookingId IN (SELECT Id FROM Bookings WHERE EventId = 4);
DELETE FROM Bookings WHERE EventId = 4;
DELETE FROM Seats WHERE EventId = 4;
DELETE FROM Tables WHERE EventId = 4;
DELETE FROM FoodItems WHERE EventId = 4;
DELETE FROM TicketTypes WHERE EventId = 4;
DELETE FROM Events WHERE Id = 4;

-- Then paste the INSERT statements generated above

-- Finally verify:
SELECT 'IMPORTED COUNTS:' as Info;
SELECT 'Events' as TableName, COUNT(*) as Count FROM Events WHERE Id = 4
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes WHERE EventId = 4
UNION ALL SELECT 'FoodItems', COUNT(*) FROM FoodItems WHERE EventId = 4
UNION ALL SELECT 'Seats', COUNT(*) FROM Seats WHERE EventId = 4;
*/
