-- ================================================================
-- STEP 1: ADD COLUMNS ONLY
-- ================================================================

PRINT 'Adding RefundedAt and RefundedBy columns...'

-- Add columns to Bookings table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'RefundedAt')
BEGIN
    ALTER TABLE Bookings ADD RefundedAt DATETIME2 NULL
    PRINT '✅ Added RefundedAt to Bookings'
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'RefundedBy')
BEGIN
    ALTER TABLE Bookings ADD RefundedBy NVARCHAR(450) NULL
    PRINT '✅ Added RefundedBy to Bookings'
END

-- Add columns to BookingLineItems table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BookingLineItems' AND COLUMN_NAME = 'RefundedAt')
BEGIN
    ALTER TABLE BookingLineItems ADD RefundedAt DATETIME2 NULL
    PRINT '✅ Added RefundedAt to BookingLineItems'
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'BookingLineItems' AND COLUMN_NAME = 'RefundedBy')
BEGIN
    ALTER TABLE BookingLineItems ADD RefundedBy NVARCHAR(450) NULL
    PRINT '✅ Added RefundedBy to BookingLineItems'
END

-- Add columns to OrganizerTicketPayments table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'OrganizerTicketPayments' AND COLUMN_NAME = 'RefundedAt')
BEGIN
    ALTER TABLE OrganizerTicketPayments ADD RefundedAt DATETIME2 NULL
    PRINT '✅ Added RefundedAt to OrganizerTicketPayments'
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'OrganizerTicketPayments' AND COLUMN_NAME = 'RefundedBy')
BEGIN
    ALTER TABLE OrganizerTicketPayments ADD RefundedBy NVARCHAR(450) NULL
    PRINT '✅ Added RefundedBy to OrganizerTicketPayments'
END

PRINT 'Step 1 completed - All columns added!'
