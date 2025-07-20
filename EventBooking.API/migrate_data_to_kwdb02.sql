-- Data Migration Script: kwdb01 → kwdb02
-- Migrating to clean BookingLineItems architecture

-- ==============================================
-- Step 1: Migrate Core Business Data
-- ==============================================

USE kwdb02;

-- 1.1 Migrate AspNetUsers (needed for foreign keys)
INSERT INTO [AspNetUsers] 
SELECT * FROM [kwdb01].[dbo].[AspNetUsers];

-- 1.2 Migrate AspNetRoles
INSERT INTO [AspNetRoles] 
SELECT * FROM [kwdb01].[dbo].[AspNetRoles];

-- 1.3 Migrate AspNetUserRoles  
INSERT INTO [AspNetUserRoles] 
SELECT * FROM [kwdb01].[dbo].[AspNetUserRoles];

-- 1.4 Migrate Organizers
INSERT INTO [Organizers]
SELECT * FROM [kwdb01].[dbo].[Organizers];

-- 1.5 Migrate Venues
INSERT INTO [Venues] 
SELECT * FROM [kwdb01].[dbo].[Venues];

-- 1.6 Migrate Events
INSERT INTO [Events] 
SELECT * FROM [kwdb01].[dbo].[Events];

-- 1.7 Migrate TicketTypes
INSERT INTO [TicketTypes] 
SELECT * FROM [kwdb01].[dbo].[TicketTypes];

-- 1.8 Migrate FoodItems
INSERT INTO [FoodItems] 
SELECT * FROM [kwdb01].[dbo].[FoodItems];

-- 1.9 Migrate Seats (if using allocated seating)
INSERT INTO [Seats] 
SELECT * FROM [kwdb01].[dbo].[Seats];

-- 1.10 Migrate Tables (if using table seating)
INSERT INTO [Tables] 
SELECT * FROM [kwdb01].[dbo].[Tables];

-- ==============================================
-- Step 2: Migrate and Consolidate Booking Data
-- ==============================================

-- 2.1 First, migrate consolidated booking records
-- Combine Bookings + Payment data into new Bookings table
INSERT INTO [Bookings] (
    [EventId],
    [CustomerEmail], 
    [CustomerFirstName],
    [CustomerLastName],
    [CustomerMobile],
    [PaymentIntentId],
    [PaymentStatus],
    [TotalAmount],
    [ProcessingFee],
    [Currency],
    [CreatedAt],
    [UpdatedAt],
    [Status],
    [Metadata]
)
SELECT DISTINCT
    b.EventId,
    COALESCE(p.Email, 'unknown@example.com') as CustomerEmail,
    COALESCE(p.FirstName, 'Unknown') as CustomerFirstName, 
    COALESCE(p.LastName, 'Customer') as CustomerLastName,
    p.Mobile as CustomerMobile,
    b.PaymentIntentId,
    CASE WHEN p.Id IS NOT NULL THEN 'Completed' ELSE 'Pending' END as PaymentStatus,
    b.TotalAmount,
    0 as ProcessingFee,
    'NZD' as Currency,
    b.CreatedAt,
    NULL as UpdatedAt,
    'Active' as Status,
    NULL as Metadata
FROM [kwdb01].[dbo].[Bookings] b
LEFT JOIN [kwdb01].[dbo].[Payments] p ON b.PaymentIntentId = p.PaymentIntentId;

-- 2.2 Migrate BookingTickets as BookingLineItems
INSERT INTO [BookingLineItems] (
    [BookingId],
    [ItemType],
    [ItemId], 
    [ItemName],
    [Quantity],
    [UnitPrice],
    [TotalPrice],
    [SeatDetails],
    [ItemDetails],
    [QRCode],
    [Status],
    [CreatedAt]
)
SELECT 
    nb.Id as BookingId,
    'Ticket' as ItemType,
    bt.TicketTypeId as ItemId,
    tt.Name as ItemName,
    bt.Quantity,
    COALESCE(tt.Price, 0) as UnitPrice,
    bt.Quantity * COALESCE(tt.Price, 0) as TotalPrice,
    '{"ticketTypeId":' + CAST(bt.TicketTypeId as NVARCHAR(10)) + ',"type":"' + COALESCE(tt.Type, '') + '"}' as SeatDetails,
    '{"originalBookingTicketId":' + CAST(bt.Id as NVARCHAR(10)) + '}' as ItemDetails,
    eb.PaymentGUID as QRCode, -- Use existing QR codes where available
    'Active' as Status,
    bt.CreatedAt
FROM [kwdb02].[dbo].[Bookings] nb
INNER JOIN [kwdb01].[dbo].[Bookings] ob ON nb.PaymentIntentId = ob.PaymentIntentId  
INNER JOIN [kwdb01].[dbo].[BookingTickets] bt ON ob.Id = bt.BookingId
INNER JOIN [kwdb01].[dbo].[TicketTypes] tt ON bt.TicketTypeId = tt.Id
LEFT JOIN [kwdb01].[dbo].[EventBookings] eb ON ob.PaymentIntentId = eb.PaymentGUID
    AND bt.TicketTypeId = eb.TicketTypeId;

-- 2.3 Migrate BookingFoods as BookingLineItems (if any exist)
INSERT INTO [BookingLineItems] (
    [BookingId],
    [ItemType], 
    [ItemId],
    [ItemName],
    [Quantity],
    [UnitPrice],
    [TotalPrice],
    [SeatDetails],
    [ItemDetails],
    [QRCode],
    [Status],
    [CreatedAt]
)
SELECT 
    nb.Id as BookingId,
    'Food' as ItemType,
    bf.FoodItemId as ItemId,
    fi.Name as ItemName,
    bf.Quantity,
    COALESCE(fi.Price, 0) as UnitPrice,
    bf.Quantity * COALESCE(fi.Price, 0) as TotalPrice,
    NULL as SeatDetails,
    '{"originalBookingFoodId":' + CAST(bf.Id as NVARCHAR(10)) + ',"description":"' + COALESCE(fi.Description, '') + '"}' as ItemDetails,
    NULL as QRCode, -- Food items don't need QR codes
    'Active' as Status,
    bf.CreatedAt
FROM [kwdb02].[dbo].[Bookings] nb
INNER JOIN [kwdb01].[dbo].[Bookings] ob ON nb.PaymentIntentId = ob.PaymentIntentId
INNER JOIN [kwdb01].[dbo].[BookingFoods] bf ON ob.Id = bf.BookingId
INNER JOIN [kwdb01].[dbo].[FoodItems] fi ON bf.FoodItemId = fi.Id;

-- ==============================================
-- Step 3: Data Verification
-- ==============================================

-- 3.1 Compare record counts
SELECT 'Migration Summary' as Summary;

SELECT 'Bookings Migrated' as ItemType, COUNT(*) as Count 
FROM [Bookings];

SELECT 'BookingLineItems - Tickets' as ItemType, COUNT(*) as Count 
FROM [BookingLineItems] WHERE ItemType = 'Ticket';

SELECT 'BookingLineItems - Food' as ItemType, COUNT(*) as Count 
FROM [BookingLineItems] WHERE ItemType = 'Food';

-- 3.2 Verify data integrity
SELECT 'Bookings without line items' as Issue, COUNT(*) as Count
FROM [Bookings] b
LEFT JOIN [BookingLineItems] bli ON b.Id = bli.BookingId
WHERE bli.BookingId IS NULL;

-- 3.3 Check QR code migration
SELECT 'Line items with QR codes' as ItemType, COUNT(*) as Count
FROM [BookingLineItems] 
WHERE QRCode IS NOT NULL AND QRCode != '';

PRINT 'Data migration completed successfully!';
PRINT 'Verify the results above before switching the application.';
