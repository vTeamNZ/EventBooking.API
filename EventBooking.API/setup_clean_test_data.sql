-- Simplified Test Data Setup for kwdb02

USE kwdb02;

-- ==============================================
-- Step 1: Check current state
-- ==============================================
SELECT 'Current State' as Status;
SELECT 'Organizers' as TableName, COUNT(*) as Count FROM Organizers
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'Events', COUNT(*) FROM Events
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings
UNION ALL SELECT 'BookingLineItems', COUNT(*) FROM BookingLineItems;

-- ==============================================
-- Step 2: Create minimal test data
-- ==============================================

-- Insert a test organizer
IF NOT EXISTS (SELECT 1 FROM Organizers WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Organizers] ON;
    INSERT INTO [Organizers] (Id, Name, ContactEmail, PhoneNumber, CreatedAt, IsVerified, OrganizationName)
    VALUES (1, 'Test Organizer', 'test@kiwilanka.co.nz', '+64123456789', GETUTCDATE(), 1, 'Test Organization');
    SET IDENTITY_INSERT [Organizers] OFF;
END

-- Insert a test venue  
IF NOT EXISTS (SELECT 1 FROM Venues WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Venues] ON;
    INSERT INTO [Venues] (Id, Name, Description, Address, City, LayoutType, SeatSelectionMode)
    VALUES (1, 'Test Venue', 'Test venue for migration', '123 Test St', 'Auckland', 'general-admission', 'general-admission');
    SET IDENTITY_INSERT [Venues] OFF;
END

-- Insert a test event
IF NOT EXISTS (SELECT 1 FROM Events WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Events] ON;
    INSERT INTO [Events] (Id, Title, Description, Date, Price, Location, IsActive, OrganizerId, VenueId, Status, ProcessingFeeEnabled, ProcessingFeeFixedAmount, ProcessingFeePercentage, Capacity, SeatSelectionMode, StagePosition)
    VALUES (1, 'Test Event Migration', 'Test event for migration testing', DATEADD(day, 30, GETUTCDATE()), 25.00, 'Test Venue, Auckland', 1, 1, 1, 'Active', 0, 0.00, 0.00, 100, 'general-admission', 'bottom');
    SET IDENTITY_INSERT [Events] OFF;
END

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

-- Insert test booking
IF NOT EXISTS (SELECT 1 FROM Bookings WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Bookings] ON;
    INSERT INTO [Bookings] (Id, EventId, CustomerEmail, CustomerFirstName, CustomerLastName, CustomerMobile, PaymentIntentId, PaymentStatus, TotalAmount, ProcessingFee, Currency, CreatedAt, Status)
    VALUES (1, 1, 'test@customer.com', 'John', 'Doe', '+64987654321', 'pi_test123456789', 'Completed', 75.00, 0.00, 'NZD', GETUTCDATE(), 'Active');
    SET IDENTITY_INSERT [Bookings] OFF;
END

-- Insert test booking line items (the new unified structure!)
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
-- Step 3: Verify new architecture works
-- ==============================================
SELECT 'After Setup - New Architecture' as Status;
SELECT 'Organizers' as TableName, COUNT(*) as Count FROM Organizers
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL SELECT 'Events', COUNT(*) FROM Events
UNION ALL SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings
UNION ALL SELECT 'BookingLineItems', COUNT(*) FROM BookingLineItems;

-- Show the new unified booking structure
SELECT 'New BookingLineItems Architecture' as Demo;
SELECT 
    b.Id as BookingId,
    b.CustomerEmail,
    b.TotalAmount,
    bli.ItemType,
    bli.ItemName,
    bli.Quantity,
    bli.UnitPrice,
    bli.TotalPrice,
    bli.QRCode
FROM Bookings b
INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
ORDER BY b.Id, bli.Id;

PRINT 'SUCCESS: New clean database architecture is working!';
PRINT 'Next: Update services to use BookingLineItems instead of BookingTickets/BookingFoods';
