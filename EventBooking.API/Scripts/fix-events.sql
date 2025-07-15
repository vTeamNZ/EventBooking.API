-- Fix the event insertion
USE kwdb01;
GO

-- Check current organizers
SELECT Id, Name FROM Organizers;

-- Insert event using the correct organizer ID
INSERT INTO Events (Title, Description, Date, Location, Price, Capacity, OrganizerId, VenueId, ImageUrl, IsActive, SeatSelectionMode)
VALUES ('Test Event', 'A test event to verify the API works', DATEADD(DAY, 30, GETDATE()), 'Test Location', 50.00, 100, 
        (SELECT TOP 1 Id FROM Organizers), (SELECT TOP 1 Id FROM Venues), 'https://example.com/test.jpg', 1, 3);

-- Insert ticket type using the correct event ID
INSERT INTO TicketTypes (Type, Name, Price, Description, EventId, Color)
VALUES ('Standard', 'Standard Ticket', 50.00, 'Standard admission', 
        (SELECT TOP 1 Id FROM Events), '#007bff');

-- Insert a food item
INSERT INTO FoodItems (Name, Price, Description, EventId)
VALUES ('Test Food', 15.00, 'Test food item', (SELECT TOP 1 Id FROM Events));

-- Verify final results
SELECT 'Events' as TableName, COUNT(*) as RecordCount FROM Events
UNION ALL
SELECT 'Organizers', COUNT(*) FROM Organizers
UNION ALL
SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL
SELECT 'FoodItems', COUNT(*) FROM FoodItems
UNION ALL
SELECT 'Venues', COUNT(*) FROM Venues;

PRINT 'Database now has test data! Try the API:';
PRINT 'GET https://kiwilanka.co.nz/api/Events';
