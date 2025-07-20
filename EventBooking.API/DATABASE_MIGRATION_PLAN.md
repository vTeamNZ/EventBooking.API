# Clean Database Migration Plan - kwdb02

## Overview
Creating a new clean database `kwdb02` with the modern two-table BookingLineItems architecture, eliminating legacy issues and implementing industry standards.

## New Database Architecture

### Phase 1: Core Two-Table Design

#### 1. **Bookings Table** (Master Record)
```sql
CREATE TABLE [Bookings] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [EventId] int NOT NULL,
    [CustomerEmail] nvarchar(255) NOT NULL,
    [CustomerFirstName] nvarchar(100) NOT NULL,
    [CustomerLastName] nvarchar(100) NOT NULL,
    [CustomerMobile] nvarchar(20) NULL,
    [PaymentIntentId] nvarchar(255) NOT NULL,
    [PaymentStatus] nvarchar(50) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [ProcessingFee] decimal(18,2) NOT NULL DEFAULT 0,
    [Currency] nvarchar(10) NOT NULL DEFAULT 'NZD',
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    [UpdatedAt] datetime2 NULL,
    [Status] nvarchar(50) NOT NULL DEFAULT 'Active',
    [Metadata] nvarchar(max) NULL, -- JSON for extensibility
    CONSTRAINT [FK_Bookings_Events] FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);
```

#### 2. **BookingLineItems Table** (Detail Records)
```sql
CREATE TABLE [BookingLineItems] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [BookingId] int NOT NULL,
    [ItemType] nvarchar(20) NOT NULL, -- 'Ticket', 'Food', 'Merchandise'
    [ItemId] int NOT NULL, -- TicketTypeId or FoodItemId
    [ItemName] nvarchar(255) NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [TotalPrice] decimal(18,2) NOT NULL,
    [SeatDetails] nvarchar(max) NULL, -- JSON: {"row": "A", "number": 1, "seatId": 123}
    [ItemDetails] nvarchar(max) NULL, -- JSON: Additional item-specific data
    [QRCode] nvarchar(500) NULL, -- Generated QR for tickets
    [Status] nvarchar(50) NOT NULL DEFAULT 'Active',
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [FK_BookingLineItems_Bookings] FOREIGN KEY ([BookingId]) REFERENCES [Bookings]([Id]) ON DELETE CASCADE,
    INDEX [IX_BookingLineItems_BookingId] ([BookingId]),
    INDEX [IX_BookingLineItems_ItemType_ItemId] ([ItemType], [ItemId])
);
```

## Tables to Keep (Clean Schema)

### Identity & Security
- AspNetUsers, AspNetRoles, AspNetUserClaims, etc. (Identity framework)

### Core Business
- **Events** ✅ (Main event records)
- **TicketTypes** ✅ (Ticket categories and pricing)
- **FoodItems** ✅ (Food menu items)
- **Venues** ✅ (Venue layouts and seating)
- **Organizers** ✅ (Event organizers)
- **Seats** ✅ (Individual seat definitions)
- **Tables** ✅ (Table seating for events)

### New Clean Tables
- **Bookings** 🆕 (Master booking records)
- **BookingLineItems** 🆕 (All booking details - tickets, food, etc.)

## Tables to ELIMINATE (Legacy Issues)

### ❌ **BookingTickets** 
- Replaced by BookingLineItems with ItemType='Ticket'
- Eliminates FK constraint issues

### ❌ **BookingFoods**
- Replaced by BookingLineItems with ItemType='Food' 
- Unified with ticket booking logic

### ❌ **EventBookings**
- Replaced by BookingLineItems with QRCode field
- Eliminates duplicate QR generation issues

### ❌ **PaymentRecords**
- Data consolidated into Bookings table
- Eliminates payment data duplication

### ❌ **Payments**
- Stripe payment data stored in Bookings
- Simplifies payment tracking

### ❌ **Reservations** (if exists)
- Temporary reservations handled differently
- Real-time seat selection without permanent records

## Data Migration Strategy

### 1. **Essential Data to Migrate**
```sql
-- Events (all active events)
-- TicketTypes (all ticket categories)
-- FoodItems (all food menu items)
-- Venues (venue layouts)
-- Organizers (event organizer accounts)
-- AspNetUsers (user accounts)
-- Seats/Tables (seating configurations)
```

### 2. **Booking Data Consolidation**
```sql
-- FROM: Bookings + BookingTickets + EventBookings
-- TO: Bookings + BookingLineItems
-- LOGIC: Group by PaymentIntentId, consolidate line items
```

### 3. **Sample Migration Query**
```sql
-- Create consolidated booking with line items
INSERT INTO [kwdb02].[dbo].[Bookings] 
SELECT 
    b.EventId,
    p.Email,
    p.FirstName,
    p.LastName,
    p.Mobile,
    b.PaymentIntentId,
    'Completed',
    b.TotalAmount,
    0 as ProcessingFee,
    'NZD',
    b.CreatedAt,
    NULL,
    'Active',
    NULL
FROM [kwdb01].[dbo].[Bookings] b
INNER JOIN [kwdb01].[dbo].[Payments] p ON b.PaymentIntentId = p.PaymentIntentId;

-- Add ticket line items
INSERT INTO [kwdb02].[dbo].[BookingLineItems]
SELECT 
    nb.Id as BookingId,
    'Ticket',
    bt.TicketTypeId,
    tt.Name,
    bt.Quantity,
    tt.Price,
    bt.Quantity * tt.Price,
    JSON_OBJECT('ticketTypeId', bt.TicketTypeId),
    NULL,
    eb.PaymentGUID, -- As QR code initially
    'Active',
    bt.CreatedAt
FROM [kwdb02].[dbo].[Bookings] nb
INNER JOIN [kwdb01].[dbo].[Bookings] ob ON nb.PaymentIntentId = ob.PaymentIntentId
INNER JOIN [kwdb01].[dbo].[BookingTickets] bt ON ob.Id = bt.BookingId
INNER JOIN [kwdb01].[dbo].[TicketTypes] tt ON bt.TicketTypeId = tt.Id
LEFT JOIN [kwdb01].[dbo].[EventBookings] eb ON ob.PaymentIntentId = eb.PaymentGUID;
```

## Implementation Steps

### Step 1: Create New Database
1. Create `kwdb02` database
2. Run Entity Framework migrations for clean schema
3. Create indexes and constraints

### Step 2: Migrate Core Data
1. Events, Venues, Organizers
2. TicketTypes, FoodItems
3. User accounts and roles
4. Seat/Table configurations

### Step 3: Migrate Booking Data
1. Consolidate Bookings table
2. Create BookingLineItems from BookingTickets
3. Add food items when implemented
4. Generate proper QR codes

### Step 4: Update Connection String
1. Update appsettings.json to point to kwdb02
2. Test all functionality
3. Backup kwdb01 for safety

### Step 5: Cleanup
1. Verify all data migrated correctly
2. Update any hardcoded references
3. Drop kwdb01 after confirmation

## Benefits of Clean Database

### ✅ **No Legacy Issues**
- No orphaned FK constraints
- No duplicate QR records
- No inconsistent payment data

### ✅ **Modern Architecture**
- Industry-standard two-table design
- JSON metadata for flexibility
- Proper cascading deletes

### ✅ **Future-Proof**
- Easy to add new item types (merchandise, etc.)
- Extensible JSON fields
- Clean audit trail

### ✅ **Performance**
- Optimized indexes
- No redundant tables
- Efficient queries

## Risk Mitigation

### 🔒 **Data Safety**
- Keep kwdb01 as backup during transition
- Export critical data before migration
- Test migration scripts on development copy

### 🔒 **Zero Downtime**
- Prepare kwdb02 completely offline
- Quick connection string switch
- Rollback plan ready

### 🔒 **Validation**
- Compare record counts
- Test all booking scenarios
- Verify QR generation works

This clean database approach will give us a solid, maintainable foundation without the baggage of legacy schema issues.
