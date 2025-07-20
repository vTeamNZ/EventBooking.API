-- Simple Migration Script - Core Data to kwdb02
-- Run this script section by section

-- ==============================================
-- Step 1: Check current state
-- ==============================================
USE kwdb02;

SELECT 'Before Migration' as Status;
SELECT 'Events' as TableName, COUNT(*) as Count FROM Events
UNION ALL SELECT 'Organizers', COUNT(*) FROM Organizers  
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings;

-- ==============================================
-- Step 2: Create sample core data manually
-- (for immediate testing while full migration planned)
-- ==============================================

-- Insert a test organizer
IF NOT EXISTS (SELECT 1 FROM Organizers WHERE Id = 1)
INSERT INTO [Organizers] (Id, Name, Email, Phone, Description, CreatedAt, UserId)
VALUES (1, 'Test Organizer', 'test@kiwilanka.co.nz', '+64123456789', 'Test organizer for migration', GETUTCDATE(), NULL);

-- Insert a test venue  
IF NOT EXISTS (SELECT 1 FROM Venues WHERE Id = 1)
INSERT INTO [Venues] (Id, Name, Address, City, Country, Capacity, Layout, CreatedAt)
VALUES (1, 'Test Venue', '123 Test St', 'Auckland', 'New Zealand', 100, 'general-admission', GETUTCDATE());

-- Insert a test event
IF NOT EXISTS (SELECT 1 FROM Events WHERE Id = 1)
INSERT INTO [Events] (Id, Title, Description, DateTime, Price, Location, IsActive, CreatedAt, OrganizerId, VenueId, Status)
VALUES (1, 'Test Event Migration', 'Test event for migration testing', DATEADD(day, 30, GETUTCDATE()), 25.00, 'Test Venue, Auckland', 1, GETUTCDATE(), 1, 1, 'Active');

-- Insert test ticket types
IF NOT EXISTS (SELECT 1 FROM TicketTypes WHERE EventId = 1)
BEGIN
    INSERT INTO [TicketTypes] (EventId, Name, Price, Type, Description, AvailableQuantity, SoldQuantity, Color)
    VALUES 
    (1, 'General Admission', 25.00, 'general-admission', 'General admission ticket', 50, 0, '#007bff'),
    (1, 'VIP', 50.00, 'general-admission', 'VIP ticket with perks', 20, 0, '#ffc107');
END

-- Verify core data
SELECT 'After Core Data Setup' as Status;
SELECT 'Events' as TableName, COUNT(*) as Count FROM Events
UNION ALL SELECT 'Organizers', COUNT(*) FROM Organizers  
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes;
