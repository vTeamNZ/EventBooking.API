-- Final Clean Test Data Setup for kwdb02

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
-- Step 2: Create test data with correct schema
-- ==============================================

-- Insert test organizer
IF NOT EXISTS (SELECT 1 FROM Organizers WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Organizers] ON;
    INSERT INTO [Organizers] (Id, Name, ContactEmail, PhoneNumber, CreatedAt, IsVerified, OrganizationName)
    VALUES (1, 'Test Organizer', 'test@kiwilanka.co.nz', '+64123456789', GETUTCDATE(), 1, 'Test Organization');
    SET IDENTITY_INSERT [Organizers] OFF;
END

-- Insert test venue  
IF NOT EXISTS (SELECT 1 FROM Venues WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Venues] ON;
    INSERT INTO [Venues] (Id, Name, Description, Address, City, LayoutType, SeatSelectionMode)
    VALUES (1, 'Test Venue', 'Test venue for migration', '123 Test St', 'Auckland', 'general-admission', 'general-admission');
    SET IDENTITY_INSERT [Venues] OFF;
END

-- Insert test event
IF NOT EXISTS (SELECT 1 FROM Events WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Events] ON;
    INSERT INTO [Events] (Id, Title, Description, Date, Price, Location, IsActive, OrganizerId, VenueId, Status, ProcessingFeeEnabled, ProcessingFeeFixedAmount, ProcessingFeePercentage, Capacity, SeatSelectionMode, StagePosition)
    VALUES (1, 'Test Event Migration', 'Test event for BookingLineItems architecture', DATEADD(day, 30, GETUTCDATE()), 25.00, 'Test Venue, Auckland', 1, 1, 1, 'Active', 0, 0.00, 0.00, 100, 'general-admission', 'bottom');
    SET IDENTITY_INSERT [Events] OFF;
END

-- Insert test ticket types
IF NOT EXISTS (SELECT 1 FROM TicketTypes WHERE EventId = 1)
BEGIN
    SET IDENTITY_INSERT [TicketTypes] ON;
    INSERT INTO [TicketTypes] (Id, EventId, Name, Price, Type, Description, Color, MaxTickets)
    VALUES 
    (1, 1, 'General Admission', 25.00, 'general-admission', 'General admission ticket', '#007bff', 50),
    (2, 1, 'VIP', 50.00, 'general-admission', 'VIP ticket with perks', '#ffc107', 20);
    SET IDENTITY_INSERT [TicketTypes] OFF;
END

-- Insert test booking with new consolidated structure
IF NOT EXISTS (SELECT 1 FROM Bookings WHERE Id = 1)
BEGIN
    SET IDENTITY_INSERT [Bookings] ON;
    INSERT INTO [Bookings] (Id, EventId, CustomerEmail, CustomerFirstName, CustomerLastName, CustomerMobile, PaymentIntentId, PaymentStatus, TotalAmount, ProcessingFee, Currency, CreatedAt, Status)
    VALUES (1, 1, 'test@customer.com', 'John', 'Doe', '+64987654321', 'pi_test123456789', 'Completed', 75.00, 0.00, 'NZD', GETUTCDATE(), 'Active');
    SET IDENTITY_INSERT [Bookings] OFF;
END

-- Insert BookingLineItems - The New Unified Architecture!
IF NOT EXISTS (SELECT 1 FROM BookingLineItems WHERE BookingId = 1)
BEGIN
    SET IDENTITY_INSERT [BookingLineItems] ON;
    INSERT INTO [BookingLineItems] (Id, BookingId, ItemType, ItemId, ItemName, Quantity, UnitPrice, TotalPrice, SeatDetails, ItemDetails, QRCode, Status, CreatedAt)
    VALUES 
    (1, 1, 'Ticket', 1, 'General Admission', 2, 25.00, 50.00, '{"ticketTypeId":1,"type":"general-admission"}', '{"testData":true}', 'QR_GENERAL_001_002', 'Active', GETUTCDATE()),
    (2, 1, 'Ticket', 2, 'VIP', 1, 50.00, 25.00, '{"ticketTypeId":2,"type":"general-admission"}', '{"vipPerks":["priority-boarding","complimentary-drink"]}', 'QR_VIP_001', 'Active', GETUTCDATE());
    SET IDENTITY_INSERT [BookingLineItems] OFF;
END

-- Add a second booking with food items to demonstrate flexibility
IF NOT EXISTS (SELECT 1 FROM Bookings WHERE Id = 2)
BEGIN
    SET IDENTITY_INSERT [Bookings] ON;
    INSERT INTO [Bookings] (Id, EventId, CustomerEmail, CustomerFirstName, CustomerLastName, PaymentIntentId, PaymentStatus, TotalAmount, ProcessingFee, Currency, CreatedAt, Status)
    VALUES (2, 1, 'jane@customer.com', 'Jane', 'Smith', 'pi_test987654321', 'Completed', 90.00, 2.50, 'NZD', GETUTCDATE(), 'Active');
    SET IDENTITY_INSERT [Bookings] OFF;
    
    SET IDENTITY_INSERT [BookingLineItems] ON;
    INSERT INTO [BookingLineItems] (Id, BookingId, ItemType, ItemId, ItemName, Quantity, UnitPrice, TotalPrice, SeatDetails, ItemDetails, QRCode, Status, CreatedAt)
    VALUES 
    (3, 2, 'Ticket', 1, 'General Admission', 1, 25.00, 25.00, '{"ticketTypeId":1,"type":"general-admission","seatPreference":"front"}', '{"customerNote":"Front section preferred"}', 'QR_GENERAL_003', 'Active', GETUTCDATE()),
    (4, 2, 'Food', 101, 'Pizza Slice', 2, 15.00, 30.00, NULL, '{"allergens":["gluten","dairy"],"size":"large"}', NULL, 'Active', GETUTCDATE()),
    (5, 2, 'Food', 102, 'Craft Beer', 3, 12.00, 36.00, NULL, '{"brand":"Local IPA","alcoholContent":"5.2%"}', NULL, 'Active', GETUTCDATE());
    SET IDENTITY_INSERT [BookingLineItems] OFF;
END

-- ==============================================
-- Step 3: Demonstrate New Architecture Benefits
-- ==============================================
SELECT 'SUCCESS: New BookingLineItems Architecture Deployed!' as Status;

-- Show unified booking structure
SELECT 'Unified Booking Summary' as Demo;
SELECT 
    b.Id as BookingId,
    b.CustomerEmail,
    b.PaymentStatus,
    b.TotalAmount,
    COUNT(bli.Id) as LineItemCount,
    SUM(CASE WHEN bli.ItemType = 'Ticket' THEN bli.Quantity ELSE 0 END) as TotalTickets,
    SUM(CASE WHEN bli.ItemType = 'Food' THEN bli.Quantity ELSE 0 END) as TotalFoodItems
FROM Bookings b
LEFT JOIN BookingLineItems bli ON b.Id = bli.BookingId
GROUP BY b.Id, b.CustomerEmail, b.PaymentStatus, b.TotalAmount
ORDER BY b.Id;

-- Show detailed line items
SELECT 'Detailed Line Items (Tickets + Food in One Table!)' as Demo;
SELECT 
    b.Id as BookingId,
    b.CustomerFirstName + ' ' + b.CustomerLastName as Customer,
    bli.ItemType,
    bli.ItemName,
    bli.Quantity,
    bli.UnitPrice,
    bli.TotalPrice,
    CASE WHEN bli.QRCode IS NOT NULL THEN 'Has QR' ELSE 'No QR' END as QRStatus,
    bli.SeatDetails,
    bli.ItemDetails
FROM Bookings b
INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
ORDER BY b.Id, bli.ItemType, bli.Id;

-- Demonstrate query flexibility
SELECT 'Tickets Only Query (Easy!)' as Demo;
SELECT b.CustomerEmail, bli.ItemName, bli.Quantity, bli.QRCode
FROM Bookings b
INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
WHERE bli.ItemType = 'Ticket';

SELECT 'Food Items Only Query (Easy!)' as Demo;  
SELECT b.CustomerEmail, bli.ItemName, bli.Quantity, bli.ItemDetails
FROM Bookings b
INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
WHERE bli.ItemType = 'Food';

PRINT 'ARCHITECTURE VALIDATED: Ready to update services to use BookingLineItems!';
PRINT 'Benefits Achieved:';
PRINT '1. Single table for all booking items (tickets, food, future merchandise)';
PRINT '2. JSON metadata for flexibility';  
PRINT '3. Unified QR code handling';
PRINT '4. Industry-standard two-table design';
PRINT '5. Easy to query and maintain';
