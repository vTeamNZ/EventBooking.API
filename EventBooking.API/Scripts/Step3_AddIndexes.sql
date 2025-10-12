-- ================================================================
-- STEP 3: ADD PERFORMANCE INDEXES
-- ================================================================

PRINT 'Adding performance indexes...'

-- Index for Bookings status filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Bookings_Status_EventId')
BEGIN
    CREATE INDEX IX_Bookings_Status_EventId ON Bookings(Status, EventId)
    PRINT '✅ Created IX_Bookings_Status_EventId'
END
ELSE
    PRINT '⚠️ IX_Bookings_Status_EventId already exists'

-- Index for BookingLineItems status filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_BookingLineItems_Status_ItemType')
BEGIN
    CREATE INDEX IX_BookingLineItems_Status_ItemType ON BookingLineItems(Status, ItemType)
    PRINT '✅ Created IX_BookingLineItems_Status_ItemType'
END
ELSE
    PRINT '⚠️ IX_BookingLineItems_Status_ItemType already exists'

-- Index for OrganizerTicketPayments status filtering
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizerTicketPayments_Status_EventId')
BEGIN
    CREATE INDEX IX_OrganizerTicketPayments_Status_EventId ON OrganizerTicketPayments(Status, EventId)
    PRINT '✅ Created IX_OrganizerTicketPayments_Status_EventId'
END
ELSE
    PRINT '⚠️ IX_OrganizerTicketPayments_Status_EventId already exists'

-- Index for partial refund tracking
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_OrganizerTicketPayments_BookingLineItemId_Status')
BEGIN
    CREATE INDEX IX_OrganizerTicketPayments_BookingLineItemId_Status ON OrganizerTicketPayments(BookingLineItemId, Status)
    PRINT '✅ Created IX_OrganizerTicketPayments_BookingLineItemId_Status'
END
ELSE
    PRINT '⚠️ IX_OrganizerTicketPayments_BookingLineItemId_Status already exists'

PRINT 'Step 3 completed - All performance indexes added!'
