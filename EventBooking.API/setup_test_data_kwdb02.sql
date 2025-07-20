-- Corrected Migration Script - Core Data to kwdb02

USE kwdb02;

-- ==============================================
-- Step 1: Check current state
-- ==============================================
SELECT 'Before Migration' as Status;
SELECT 'Events' as TableName, COUNT(*) as Count FROM Events
UNION ALL SELECT 'Organizers', COUNT(*) FROM Organizers  
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings;

-- ==============================================
-- Step 2: Create sample core data for testing
-- ==============================================

-- Insert a test organizer
IF NOT EXISTS (SELECT 1 FROM Organizers WHERE Id = 1)
INSERT INTO [Organizers] (Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl, CreatedAt, IsVerified, OrganizationName, Website)
VALUES (1, 'Test Organizer', 'test@kiwilanka.co.nz', '+64123456789', NULL, NULL, NULL, GETUTCDATE(), 1, 'Test Organization', 'https://kiwilanka.co.nz');

-- Insert a test venue  
IF NOT EXISTS (SELECT 1 FROM Venues WHERE Id = 1)
INSERT INTO [Venues] (Id, Name, Address, City, Country, Capacity, Layout, CreatedAt)
VALUES (1, 'Test Venue', '123 Test St', 'Auckland', 'New Zealand', 100, 'general-admission', GETUTCDATE());

-- Insert a test event
IF NOT EXISTS (SELECT 1 FROM Events WHERE Id = 1)
INSERT INTO [Events] (Id, Title, Description, Date, Price, Location, IsActive, OrganizerId, VenueId, Status, ProcessingFeeEnabled, ProcessingFeeFixedAmount, ProcessingFeePercentage, Capacity, SeatSelectionMode, StagePosition)
VALUES (1, 'Test Event Migration', 'Test event for migration testing', DATEADD(day, 30, GETUTCDATE()), 25.00, 'Test Venue, Auckland', 1, 1, 1, 'Active', 0, 0.00, 0.00, 100, 'general-admission', 'bottom');

-- Insert test ticket types
IF NOT EXISTS (SELECT 1 FROM TicketTypes WHERE EventId = 1)
BEGIN
    SET IDENTITY_INSERT [TicketTypes] ON;
    INSERT INTO [TicketTypes] (Id, EventId, Name, Price, Type, Description, AvailableQuantity, SoldQuantity, Color)
    VALUES 
    (1, 1, 'General Admission', 25.00, 'general-admission', 'General admission ticket', 50, 0, '#007bff'),
    (2, 1, 'VIP', 50.00, 'general-admission', 'VIP ticket with perks', 20, 0, '#ffc107');
    SET IDENTITY_INSERT [TicketTypes] OFF;
END

-- ==============================================
-- Step 3: Create test booking data
-- ==============================================

-- Insert test booking
IF NOT EXISTS (SELECT 1 FROM Bookings WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Bookings] ON;
    INSERT INTO [Bookings] (Id, EventId, CustomerEmail, CustomerFirstName, CustomerLastName, CustomerMobile, PaymentIntentId, PaymentStatus, TotalAmount, ProcessingFee, Currency, CreatedAt, Status)
    VALUES (1, 1, 'test@customer.com', 'John', 'Doe', '+64987654321', 'pi_test123456789', 'Completed', 75.00, 0.00, 'NZD', GETUTCDATE(), 'Active');
    SET IDENTITY_INSERT [Bookings] OFF;
END

-- Insert test booking line items
IF NOT EXISTS (SELECT 1 FROM BookingLineItems WHERE BookingId = 1)
BEGIN
    SET IDENTITY_INSERT [BookingLineItems] ON;
    INSERT INTO [BookingLineItems] (Id, BookingId, ItemType, ItemId, ItemName, Quantity, UnitPrice, TotalPrice, SeatDetails, ItemDetails, QRCode, Status, CreatedAt)
    VALUES 
    (1, 1, 'Ticket', 1, 'General Admission', 2, 25.00, 50.00, '{"ticketTypeId":1,"type":"general-admission"}', '{"testData":true}', 'TEST_QR_CODE_001', 'Active', GETUTCDATE()),
    (2, 1, 'Ticket', 2, 'VIP', 1, 50.00, 25.00, '{"ticketTypeId":2,"type":"general-admission"}', '{"testData":true}', 'TEST_QR_CODE_002', 'Active', GETUTCDATE());
    SET IDENTITY_INSERT [BookingLineItems] OFF;
END

-- ==============================================
-- Step 4: Verify setup
-- ==============================================
SELECT 'After Core Data Setup' as Status;
SELECT 'Events' as TableName, COUNT(*) as Count FROM Events
UNION ALL SELECT 'Organizers', COUNT(*) FROM Organizers  
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings
UNION ALL SELECT 'BookingLineItems', COUNT(*) FROM BookingLineItems;

-- Show sample data
SELECT 'Sample Booking Data' as Info;
SELECT b.Id, b.CustomerEmail, b.TotalAmount, b.PaymentStatus,
       COUNT(bli.Id) as LineItemCount
FROM Bookings b
LEFT JOIN BookingLineItems bli ON b.Id = bli.BookingId
GROUP BY b.Id, b.CustomerEmail, b.TotalAmount, b.PaymentStatus;

SELECT 'Sample Line Items' as Info;
SELECT bli.Id, bli.ItemType, bli.ItemName, bli.Quantity, bli.UnitPrice, bli.TotalPrice
FROM BookingLineItems bli
WHERE bli.BookingId = 1;
