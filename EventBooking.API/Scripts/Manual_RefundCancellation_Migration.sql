-- ================================================================
-- REFUND CANCELLATION ENHANCEMENT - MANUAL SQL SCRIPT
-- Date: October 10, 2025
-- Purpose: Add refund tracking columns and indexes to support 
--          refund/cancellation functionality
-- ================================================================

-- Connection: Server=tcp:kwsqlsvr01.database.windows.net,1433;Initial Catalog=kwdb01;User ID=gayantd;Password=maGulak@143456;

PRINT 'Starting Refund Cancellation Enhancement...'
PRINT 'Current Time: ' + CONVERT(VARCHAR, GETDATE(), 121)

-- ================================================================
-- 1. ADD REFUND TRACKING COLUMNS TO BOOKINGS TABLE
-- ================================================================
PRINT ''
PRINT '1. Adding refund tracking columns to Bookings table...'

-- Check if RefundedAt column exists in Bookings table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'RefundedAt')
BEGIN
    ALTER TABLE Bookings ADD RefundedAt DATETIME2 NULL
    PRINT '   ✅ Added RefundedAt column to Bookings table'
END
ELSE
    PRINT '   ⚠️  RefundedAt column already exists in Bookings table'

-- Check if RefundedBy column exists in Bookings table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'RefundedBy')
BEGIN
    ALTER TABLE Bookings ADD RefundedBy NVARCHAR(450) NULL
    PRINT '   ✅ Added RefundedBy column to Bookings table'
END
ELSE
    PRINT '   ⚠️  RefundedBy column already exists in Bookings table'

-- ================================================================
-- 2. ADD REFUND TRACKING COLUMNS TO BOOKINGLINEITEMS TABLE
-- ================================================================
PRINT ''
PRINT '2. Adding refund tracking columns to BookingLineItems table...'

-- Check if RefundedAt column exists in BookingLineItems table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BookingLineItems' AND COLUMN_NAME = 'RefundedAt')
BEGIN
    ALTER TABLE BookingLineItems ADD RefundedAt DATETIME2 NULL
    PRINT '   ✅ Added RefundedAt column to BookingLineItems table'
END
ELSE
    PRINT '   ⚠️  RefundedAt column already exists in BookingLineItems table'

-- Check if RefundedBy column exists in BookingLineItems table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BookingLineItems' AND COLUMN_NAME = 'RefundedBy')
BEGIN
    ALTER TABLE BookingLineItems ADD RefundedBy NVARCHAR(450) NULL
    PRINT '   ✅ Added RefundedBy column to BookingLineItems table'
END
ELSE
    PRINT '   ⚠️  RefundedBy column already exists in BookingLineItems table'

-- ================================================================
-- 3. ADD REFUND TRACKING COLUMNS TO ORGANIZERTICKETPAYMENTS TABLE
-- ================================================================
PRINT ''
PRINT '3. Adding refund tracking columns to OrganizerTicketPayments table...'

-- Check if RefundedAt column exists in OrganizerTicketPayments table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'OrganizerTicketPayments' AND COLUMN_NAME = 'RefundedAt')
BEGIN
    ALTER TABLE OrganizerTicketPayments ADD RefundedAt DATETIME2 NULL
    PRINT '   ✅ Added RefundedAt column to OrganizerTicketPayments table'
END
ELSE
    PRINT '   ⚠️  RefundedAt column already exists in OrganizerTicketPayments table'

-- Check if RefundedBy column exists in OrganizerTicketPayments table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'OrganizerTicketPayments' AND COLUMN_NAME = 'RefundedBy')
BEGIN
    ALTER TABLE OrganizerTicketPayments ADD RefundedBy NVARCHAR(450) NULL
    PRINT '   ✅ Added RefundedBy column to OrganizerTicketPayments table'
END
ELSE
    PRINT '   ⚠️  RefundedBy column already exists in OrganizerTicketPayments table'

-- ================================================================
-- 4. CREATE FOREIGN KEY CONSTRAINTS
-- ================================================================
PRINT ''
PRINT '4. Creating foreign key constraints...'

-- Foreign key for Bookings.RefundedBy -> AspNetUsers.Id
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Bookings_AspNetUsers_RefundedBy')
BEGIN
    ALTER TABLE Bookings 
    ADD CONSTRAINT FK_Bookings_AspNetUsers_RefundedBy 
    FOREIGN KEY (RefundedBy) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    PRINT '   ✅ Created foreign key FK_Bookings_AspNetUsers_RefundedBy'
END
ELSE
    PRINT '   ⚠️  Foreign key FK_Bookings_AspNetUsers_RefundedBy already exists'

-- Foreign key for BookingLineItems.RefundedBy -> AspNetUsers.Id
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_BookingLineItems_AspNetUsers_RefundedBy')
BEGIN
    ALTER TABLE BookingLineItems 
    ADD CONSTRAINT FK_BookingLineItems_AspNetUsers_RefundedBy 
    FOREIGN KEY (RefundedBy) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    PRINT '   ✅ Created foreign key FK_BookingLineItems_AspNetUsers_RefundedBy'
END
ELSE
    PRINT '   ⚠️  Foreign key FK_BookingLineItems_AspNetUsers_RefundedBy already exists'

-- Foreign key for OrganizerTicketPayments.RefundedBy -> AspNetUsers.Id
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_OrganizerTicketPayments_AspNetUsers_RefundedBy')
BEGIN
    ALTER TABLE OrganizerTicketPayments 
    ADD CONSTRAINT FK_OrganizerTicketPayments_AspNetUsers_RefundedBy 
    FOREIGN KEY (RefundedBy) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    PRINT '   ✅ Created foreign key FK_OrganizerTicketPayments_AspNetUsers_RefundedBy'
END
ELSE
    PRINT '   ⚠️  Foreign key FK_OrganizerTicketPayments_AspNetUsers_RefundedBy already exists'

-- ================================================================
-- 5. CREATE PERFORMANCE INDEXES
-- ================================================================
PRINT ''
PRINT '5. Creating performance indexes...'

-- Index for Bookings status filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_Status_EventId')
BEGIN
    CREATE INDEX IX_Bookings_Status_EventId ON Bookings(Status, EventId)
    PRINT '   ✅ Created index IX_Bookings_Status_EventId'
END
ELSE
    PRINT '   ⚠️  Index IX_Bookings_Status_EventId already exists'

-- Index for BookingLineItems status filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BookingLineItems_Status_ItemType')
BEGIN
    CREATE INDEX IX_BookingLineItems_Status_ItemType ON BookingLineItems(Status, ItemType)
    PRINT '   ✅ Created index IX_BookingLineItems_Status_ItemType'
END
ELSE
    PRINT '   ⚠️  Index IX_BookingLineItems_Status_ItemType already exists'

-- Index for OrganizerTicketPayments status filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizerTicketPayments_Status_EventId')
BEGIN
    CREATE INDEX IX_OrganizerTicketPayments_Status_EventId ON OrganizerTicketPayments(Status, EventId)
    PRINT '   ✅ Created index IX_OrganizerTicketPayments_Status_EventId'
END
ELSE
    PRINT '   ⚠️  Index IX_OrganizerTicketPayments_Status_EventId already exists'

-- Index for partial refund tracking
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizerTicketPayments_BookingLineItemId_Status')
BEGIN
    CREATE INDEX IX_OrganizerTicketPayments_BookingLineItemId_Status ON OrganizerTicketPayments(BookingLineItemId, Status)
    PRINT '   ✅ Created index IX_OrganizerTicketPayments_BookingLineItemId_Status'
END
ELSE
    PRINT '   ⚠️  Index IX_OrganizerTicketPayments_BookingLineItemId_Status already exists'

-- Index for refund audit queries on Bookings
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_RefundedAt')
BEGIN
    CREATE INDEX IX_Bookings_RefundedAt ON Bookings(RefundedAt) WHERE RefundedAt IS NOT NULL
    PRINT '   ✅ Created index IX_Bookings_RefundedAt'
END
ELSE
    PRINT '   ⚠️  Index IX_Bookings_RefundedAt already exists'

-- Index for refund audit queries on BookingLineItems
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BookingLineItems_RefundedAt')
BEGIN
    CREATE INDEX IX_BookingLineItems_RefundedAt ON BookingLineItems(RefundedAt) WHERE RefundedAt IS NOT NULL
    PRINT '   ✅ Created index IX_BookingLineItems_RefundedAt'
END
ELSE
    PRINT '   ⚠️  Index IX_BookingLineItems_RefundedAt already exists'

-- Index for refund audit queries on OrganizerTicketPayments
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizerTicketPayments_RefundedAt')
BEGIN
    CREATE INDEX IX_OrganizerTicketPayments_RefundedAt ON OrganizerTicketPayments(RefundedAt) WHERE RefundedAt IS NOT NULL
    PRINT '   ✅ Created index IX_OrganizerTicketPayments_RefundedAt'
END
ELSE
    PRINT '   ⚠️  Index IX_OrganizerTicketPayments_RefundedAt already exists'

-- ================================================================
-- 6. UPDATE __EFMIGRATIONSHISTORY TABLE
-- ================================================================
PRINT ''
PRINT '6. Updating migration history...'

-- Check if our migration entry already exists
IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId = '20251010050823_AddRefundCancellationSupport')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251010050823_AddRefundCancellationSupport', '8.0.0')
    PRINT '   ✅ Added migration entry to __EFMigrationsHistory'
END
ELSE
    PRINT '   ⚠️  Migration entry already exists in __EFMigrationsHistory'

-- ================================================================
-- 7. VERIFICATION QUERIES
-- ================================================================
PRINT ''
PRINT '7. Verification - Checking all changes...'

-- Verify columns were added
PRINT ''
PRINT 'Refund tracking columns:'
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME IN ('Bookings', 'BookingLineItems', 'OrganizerTicketPayments')
  AND COLUMN_NAME IN ('RefundedAt', 'RefundedBy')
ORDER BY TABLE_NAME, COLUMN_NAME

-- Verify foreign keys
PRINT ''
PRINT 'Foreign key constraints:'
SELECT 
    fk.name AS ForeignKeyName,
    t.name AS TableName,
    c.name AS ColumnName,
    rt.name AS ReferencedTable,
    rc.name AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
INNER JOIN sys.tables rt ON fkc.referenced_object_id = rt.object_id
INNER JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
WHERE fk.name LIKE '%RefundedBy%'
ORDER BY fk.name

-- Verify indexes
PRINT ''
PRINT 'Performance indexes:'
SELECT 
    i.name AS IndexName,
    t.name AS TableName,
    i.type_desc AS IndexType
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name LIKE '%Status%' OR i.name LIKE '%RefundedAt%'
ORDER BY t.name, i.name

PRINT ''
PRINT '================================================================'
PRINT 'Refund Cancellation Enhancement completed successfully!'
PRINT 'Current Time: ' + CONVERT(VARCHAR, GETDATE(), 121)
PRINT '================================================================'
