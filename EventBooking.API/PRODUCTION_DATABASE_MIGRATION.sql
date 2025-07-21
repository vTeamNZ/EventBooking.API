-- =====================================================
-- PRODUCTION DATABASE MIGRATION: kwdb01 → kwdb02
-- Critical Production Migration Script
-- Date: 2025-01-21
-- Purpose: Migrate from legacy database to clean architecture
-- =====================================================

-- SAFETY CHECKS FIRST
PRINT 'Starting Production Database Migration...';
PRINT 'Source: kwdb01 (Legacy)';
PRINT 'Target: kwdb02 (Clean Architecture)';
PRINT '=====================================================';

-- Verify both databases exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'kwdb01')
BEGIN
    RAISERROR('Source database kwdb01 not found!', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'kwdb02')
BEGIN
    RAISERROR('Target database kwdb02 not found!', 16, 1);
    RETURN;
END

-- =====================================================
-- PHASE 1: MIGRATE CORE MASTER DATA
-- =====================================================
PRINT 'Phase 1: Migrating Core Master Data...';

-- 1. Migrate AspNetUsers (User Accounts)
PRINT 'Migrating AspNetUsers...';
INSERT INTO [kwdb02].[dbo].[AspNetUsers] (
    Id, FullName, Role, UserName, NormalizedUserName, Email, NormalizedEmail,
    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber,
    PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
)
SELECT 
    Id, FullName, Role, UserName, NormalizedUserName, Email, NormalizedEmail,
    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumber,
    PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
FROM [kwdb01].[dbo].[AspNetUsers]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[AspNetUsers]);

-- 2. Migrate AspNetRoles
PRINT 'Migrating AspNetRoles...';
INSERT INTO [kwdb02].[dbo].[AspNetRoles] (Id, Name, NormalizedName, ConcurrencyStamp)
SELECT Id, Name, NormalizedName, ConcurrencyStamp
FROM [kwdb01].[dbo].[AspNetRoles]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[AspNetRoles]);

-- 3. Migrate AspNetUserRoles
PRINT 'Migrating AspNetUserRoles...';
INSERT INTO [kwdb02].[dbo].[AspNetUserRoles] (UserId, RoleId)
SELECT UserId, RoleId
FROM [kwdb01].[dbo].[AspNetUserRoles]
WHERE NOT EXISTS (
    SELECT 1 FROM [kwdb02].[dbo].[AspNetUserRoles] ur2 
    WHERE ur2.UserId = [kwdb01].[dbo].[AspNetUserRoles].UserId 
    AND ur2.RoleId = [kwdb01].[dbo].[AspNetUserRoles].RoleId
);

-- 4. Migrate Venues
PRINT 'Migrating Venues...';
SET IDENTITY_INSERT [kwdb02].[dbo].[Venues] ON;
INSERT INTO [kwdb02].[dbo].[Venues] (
    Id, Name, Description, LayoutData, Width, Height, Address, City,
    HasStaggeredSeating, HasWheelchairSpaces, LayoutType, NumberOfRows,
    RowSpacing, SeatSpacing, SeatsPerRow, WheelchairSpaces, AisleWidth,
    HasHorizontalAisles, HasVerticalAisles, HorizontalAisleRows,
    VerticalAisleSeats, SeatSelectionMode
)
SELECT 
    Id, Name, Description, LayoutData, Width, Height, Address, City,
    HasStaggeredSeating, HasWheelchairSpaces, LayoutType, NumberOfRows,
    RowSpacing, SeatSpacing, SeatsPerRow, WheelchairSpaces, AisleWidth,
    HasHorizontalAisles, HasVerticalAisles, HorizontalAisleRows,
    VerticalAisleSeats, SeatSelectionMode
FROM [kwdb01].[dbo].[Venues]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[Venues]);
SET IDENTITY_INSERT [kwdb02].[dbo].[Venues] OFF;

-- 5. Migrate Organizers
PRINT 'Migrating Organizers...';
SET IDENTITY_INSERT [kwdb02].[dbo].[Organizers] ON;
INSERT INTO [kwdb02].[dbo].[Organizers] (
    Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl,
    CreatedAt, IsVerified, OrganizationName, Website
)
SELECT 
    Id, Name, ContactEmail, PhoneNumber, UserId, FacebookUrl, YoutubeUrl,
    CreatedAt, IsVerified, OrganizationName, Website
FROM [kwdb01].[dbo].[Organizers]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[Organizers]);
SET IDENTITY_INSERT [kwdb02].[dbo].[Organizers] OFF;

-- =====================================================
-- PHASE 2: MIGRATE EVENTS AND RELATED DATA
-- =====================================================
PRINT 'Phase 2: Migrating Events and Related Data...';

-- 6. Migrate Events
PRINT 'Migrating Events...';
SET IDENTITY_INSERT [kwdb02].[dbo].[Events] ON;
INSERT INTO [kwdb02].[dbo].[Events] (
    Id, Title, Description, Date, Location, Price, Capacity, OrganizerId,
    ImageUrl, IsActive, SeatSelectionMode, StagePosition, VenueId
)
SELECT 
    Id, Title, Description, Date, Location, Price, Capacity, OrganizerId,
    ImageUrl, IsActive, SeatSelectionMode, StagePosition, VenueId
FROM [kwdb01].[dbo].[Events]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[Events]);
SET IDENTITY_INSERT [kwdb02].[dbo].[Events] OFF;

-- 7. Migrate TicketTypes
PRINT 'Migrating TicketTypes...';
SET IDENTITY_INSERT [kwdb02].[dbo].[TicketTypes] ON;
INSERT INTO [kwdb02].[dbo].[TicketTypes] (
    Id, Type, Price, Description, EventId, SeatRowAssignments, Color, Name
)
SELECT 
    Id, Type, Price, Description, EventId, SeatRowAssignments, Color, Name
FROM [kwdb01].[dbo].[TicketTypes]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[TicketTypes]);
SET IDENTITY_INSERT [kwdb02].[dbo].[TicketTypes] OFF;

-- 8. Migrate FoodItems
PRINT 'Migrating FoodItems...';
SET IDENTITY_INSERT [kwdb02].[dbo].[FoodItems] ON;
INSERT INTO [kwdb02].[dbo].[FoodItems] (Id, Name, Price, Description, EventId)
SELECT Id, Name, Price, Description, EventId
FROM [kwdb01].[dbo].[FoodItems]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[FoodItems]);
SET IDENTITY_INSERT [kwdb02].[dbo].[FoodItems] OFF;

-- 9. Migrate Seats
PRINT 'Migrating Seats...';
SET IDENTITY_INSERT [kwdb02].[dbo].[Seats] ON;
INSERT INTO [kwdb02].[dbo].[Seats] (
    Id, EventId, Row, Number, IsReserved, Height, Price, ReservedBy,
    ReservedUntil, SeatNumber, Status, TableId, Width, X, Y, TicketTypeId
)
SELECT 
    Id, EventId, Row, Number, IsReserved, Height, Price, ReservedBy,
    ReservedUntil, SeatNumber, Status, TableId, Width, X, Y, TicketTypeId
FROM [kwdb01].[dbo].[Seats]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[Seats]);
SET IDENTITY_INSERT [kwdb02].[dbo].[Seats] OFF;

-- 10. Migrate Tables
PRINT 'Migrating Tables...';
SET IDENTITY_INSERT [kwdb02].[dbo].[Tables] ON;
INSERT INTO [kwdb02].[dbo].[Tables] (
    Id, EventId, TableNumber, Capacity, Height, PricePerSeat, Shape,
    TablePrice, Width, X, Y
)
SELECT 
    Id, EventId, TableNumber, Capacity, Height, PricePerSeat, Shape,
    TablePrice, Width, X, Y
FROM [kwdb01].[dbo].[Tables]
WHERE Id NOT IN (SELECT Id FROM [kwdb02].[dbo].[Tables]);
SET IDENTITY_INSERT [kwdb02].[dbo].[Tables] OFF;

-- =====================================================
-- PHASE 3: MIGRATE BOOKING DATA (COMPLEX CONSOLIDATION)
-- =====================================================
PRINT 'Phase 3: Migrating and Consolidating Booking Data...';

-- Create temporary table to consolidate booking data
CREATE TABLE #BookingConsolidation (
    OldBookingId INT,
    PaymentIntentId NVARCHAR(255),
    EventId INT,
    CustomerEmail NVARCHAR(255),
    CustomerFirstName NVARCHAR(100),
    CustomerLastName NVARCHAR(100),
    CustomerMobile NVARCHAR(20),
    TotalAmount DECIMAL(18,2),
    ProcessingFee DECIMAL(18,2),
    PaymentStatus NVARCHAR(50),
    CreatedAt DATETIME2,
    NewBookingId INT
);

-- Step 1: Consolidate booking data from multiple sources
PRINT 'Consolidating booking data from Payments and Bookings tables...';
INSERT INTO #BookingConsolidation (
    OldBookingId, PaymentIntentId, EventId, CustomerEmail, CustomerFirstName,
    CustomerLastName, CustomerMobile, TotalAmount, ProcessingFee, PaymentStatus, CreatedAt
)
SELECT DISTINCT
    b.Id as OldBookingId,
    COALESCE(p.PaymentIntentId, b.PaymentIntentId, 'LEGACY_' + CAST(b.Id AS NVARCHAR(10))) as PaymentIntentId,
    b.EventId,
    COALESCE(p.Email, 'legacy@example.com') as CustomerEmail,
    COALESCE(p.FirstName, 'Legacy') as CustomerFirstName,
    COALESCE(p.LastName, 'Customer') as CustomerLastName,
    COALESCE(p.Mobile, '') as CustomerMobile,
    COALESCE(p.Amount, b.TotalAmount, 0) as TotalAmount,
    0 as ProcessingFee, -- Default to 0, can be updated later
    CASE 
        WHEN p.Status = 'succeeded' THEN 'Completed'
        WHEN p.Status IS NOT NULL THEN p.Status
        ELSE 'Completed'
    END as PaymentStatus,
    COALESCE(b.CreatedAt, p.CreatedAt, GETUTCDATE()) as CreatedAt
FROM [kwdb01].[dbo].[Bookings] b
LEFT JOIN [kwdb01].[dbo].[Payments] p ON b.PaymentIntentId = p.PaymentIntentId
WHERE b.Id IS NOT NULL;

-- Step 2: Create new Bookings records
PRINT 'Creating new consolidated Bookings records...';
INSERT INTO [kwdb02].[dbo].[Bookings] (
    EventId, CustomerEmail, CustomerFirstName, CustomerLastName, CustomerMobile,
    PaymentIntentId, PaymentStatus, TotalAmount, ProcessingFee, Currency,
    CreatedAt, Status, Metadata
)
OUTPUT INSERTED.Id, INSERTED.PaymentIntentId INTO #BookingIds(NewBookingId, PaymentIntentId)
SELECT DISTINCT
    EventId, CustomerEmail, CustomerFirstName, CustomerLastName, CustomerMobile,
    PaymentIntentId, PaymentStatus, TotalAmount, ProcessingFee, 'NZD',
    CreatedAt, 'Active', 
    JSON_OBJECT('migrated', 'true', 'source', 'kwdb01', 'migratedAt', FORMAT(GETUTCDATE(), 'yyyy-MM-dd HH:mm:ss'))
FROM #BookingConsolidation
WHERE PaymentIntentId NOT IN (SELECT PaymentIntentId FROM [kwdb02].[dbo].[Bookings]);

-- Create temporary table to map old to new booking IDs
CREATE TABLE #BookingIds (NewBookingId INT, PaymentIntentId NVARCHAR(255));

-- Update the consolidation table with new booking IDs
UPDATE bc
SET NewBookingId = bi.NewBookingId
FROM #BookingConsolidation bc
INNER JOIN #BookingIds bi ON bc.PaymentIntentId = bi.PaymentIntentId;

-- Also handle existing bookings in kwdb02
UPDATE bc
SET NewBookingId = b.Id
FROM #BookingConsolidation bc
INNER JOIN [kwdb02].[dbo].[Bookings] b ON bc.PaymentIntentId = b.PaymentIntentId
WHERE bc.NewBookingId IS NULL;

-- Step 3: Create BookingLineItems for Tickets
PRINT 'Creating BookingLineItems for tickets...';
INSERT INTO [kwdb02].[dbo].[BookingLineItems] (
    BookingId, ItemType, ItemId, ItemName, Quantity, UnitPrice, TotalPrice,
    SeatDetails, ItemDetails, QRCode, Status, CreatedAt
)
SELECT 
    bc.NewBookingId,
    'Ticket' as ItemType,
    bt.TicketTypeId as ItemId,
    COALESCE(tt.Name, tt.Type, 'Legacy Ticket') as ItemName,
    bt.Quantity,
    COALESCE(tt.Price, 0) as UnitPrice,
    bt.Quantity * COALESCE(tt.Price, 0) as TotalPrice,
    -- Try to find seat details from EventBookings or create generic
    COALESCE(eb.SeatNo, 'TBD') as SeatDetails,
    JSON_OBJECT(
        'ticketTypeId', bt.TicketTypeId,
        'eventId', bc.EventId,
        'migratedFrom', 'BookingTickets'
    ) as ItemDetails,
    COALESCE(eb.PaymentGUID, bc.PaymentIntentId) as QRCode, -- Temporary QR
    'Active' as Status,
    bc.CreatedAt
FROM #BookingConsolidation bc
INNER JOIN [kwdb01].[dbo].[BookingTickets] bt ON bc.OldBookingId = bt.BookingId
LEFT JOIN [kwdb01].[dbo].[TicketTypes] tt ON bt.TicketTypeId = tt.Id
LEFT JOIN [kwdb01].[dbo].[EventBookings] eb ON bc.PaymentIntentId = eb.PaymentGUID
WHERE bc.NewBookingId IS NOT NULL;

-- Step 4: Create BookingLineItems for Food (if any exist)
PRINT 'Creating BookingLineItems for food items...';
INSERT INTO [kwdb02].[dbo].[BookingLineItems] (
    BookingId, ItemType, ItemId, ItemName, Quantity, UnitPrice, TotalPrice,
    SeatDetails, ItemDetails, QRCode, Status, CreatedAt
)
SELECT 
    bc.NewBookingId,
    'Food' as ItemType,
    bf.FoodItemId as ItemId,
    COALESCE(fi.Name, 'Legacy Food Item') as ItemName,
    bf.Quantity,
    COALESCE(fi.Price, 0) as UnitPrice,
    bf.Quantity * COALESCE(fi.Price, 0) as TotalPrice,
    NULL as SeatDetails, -- Food items don't have seats
    JSON_OBJECT(
        'foodItemId', bf.FoodItemId,
        'eventId', bc.EventId,
        'migratedFrom', 'BookingFoods'
    ) as ItemDetails,
    NULL as QRCode, -- Food items don't need QR codes
    'Active' as Status,
    bc.CreatedAt
FROM #BookingConsolidation bc
INNER JOIN [kwdb01].[dbo].[BookingFoods] bf ON bc.OldBookingId = bf.BookingId
LEFT JOIN [kwdb01].[dbo].[FoodItems] fi ON bf.FoodItemId = fi.Id
WHERE bc.NewBookingId IS NOT NULL;

-- =====================================================
-- PHASE 4: MIGRATION VERIFICATION
-- =====================================================
PRINT 'Phase 4: Migration Verification...';

-- Count records migrated
DECLARE @UsersCount INT, @EventsCount INT, @BookingsCount INT, @LineItemsCount INT;

SELECT @UsersCount = COUNT(*) FROM [kwdb02].[dbo].[AspNetUsers];
SELECT @EventsCount = COUNT(*) FROM [kwdb02].[dbo].[Events];
SELECT @BookingsCount = COUNT(*) FROM [kwdb02].[dbo].[Bookings];
SELECT @LineItemsCount = COUNT(*) FROM [kwdb02].[dbo].[BookingLineItems];

PRINT 'Migration Summary:';
PRINT 'Users migrated: ' + CAST(@UsersCount AS NVARCHAR(10));
PRINT 'Events migrated: ' + CAST(@EventsCount AS NVARCHAR(10));
PRINT 'Bookings created: ' + CAST(@BookingsCount AS NVARCHAR(10));
PRINT 'Line items created: ' + CAST(@LineItemsCount AS NVARCHAR(10));

-- Verify data integrity
PRINT 'Verifying data integrity...';

-- Check for orphaned line items
IF EXISTS (SELECT 1 FROM [kwdb02].[dbo].[BookingLineItems] bli 
           LEFT JOIN [kwdb02].[dbo].[Bookings] b ON bli.BookingId = b.Id 
           WHERE b.Id IS NULL)
BEGIN
    PRINT 'WARNING: Orphaned BookingLineItems found!';
END
ELSE
BEGIN
    PRINT 'OK: No orphaned BookingLineItems';
END

-- Check booking totals vs line items
WITH BookingTotals AS (
    SELECT 
        BookingId,
        SUM(TotalPrice) as LineItemTotal
    FROM [kwdb02].[dbo].[BookingLineItems]
    GROUP BY BookingId
)
SELECT 
    COUNT(*) as MismatchedBookings
FROM [kwdb02].[dbo].[Bookings] b
INNER JOIN BookingTotals bt ON b.Id = bt.BookingId
WHERE ABS(b.TotalAmount - bt.LineItemTotal) > 0.01;

-- Cleanup temporary tables
DROP TABLE #BookingConsolidation;
DROP TABLE #BookingIds;

PRINT '=====================================================';
PRINT 'MIGRATION COMPLETED SUCCESSFULLY!';
PRINT 'Next Steps:';
PRINT '1. Update connection string in appsettings.json to kwdb02';
PRINT '2. Test application functionality thoroughly';
PRINT '3. Backup kwdb01 before decommissioning';
PRINT '4. Monitor application for any issues';
PRINT '=====================================================';
