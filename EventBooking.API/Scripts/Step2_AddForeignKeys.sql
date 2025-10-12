-- ================================================================
-- STEP 2: ADD FOREIGN KEY CONSTRAINTS
-- ================================================================

PRINT 'Adding foreign key constraints...'

-- Foreign key for Bookings.RefundedBy -> AspNetUsers.Id
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Bookings_AspNetUsers_RefundedBy')
BEGIN
    ALTER TABLE Bookings 
    ADD CONSTRAINT FK_Bookings_AspNetUsers_RefundedBy 
    FOREIGN KEY (RefundedBy) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    PRINT '✅ Created FK_Bookings_AspNetUsers_RefundedBy'
END
ELSE
    PRINT '⚠️ FK_Bookings_AspNetUsers_RefundedBy already exists'

-- Foreign key for BookingLineItems.RefundedBy -> AspNetUsers.Id
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_BookingLineItems_AspNetUsers_RefundedBy')
BEGIN
    ALTER TABLE BookingLineItems 
    ADD CONSTRAINT FK_BookingLineItems_AspNetUsers_RefundedBy 
    FOREIGN KEY (RefundedBy) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    PRINT '✅ Created FK_BookingLineItems_AspNetUsers_RefundedBy'
END
ELSE
    PRINT '⚠️ FK_BookingLineItems_AspNetUsers_RefundedBy already exists'

-- Foreign key for OrganizerTicketPayments.RefundedBy -> AspNetUsers.Id
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_OrganizerTicketPayments_AspNetUsers_RefundedBy')
BEGIN
    ALTER TABLE OrganizerTicketPayments 
    ADD CONSTRAINT FK_OrganizerTicketPayments_AspNetUsers_RefundedBy 
    FOREIGN KEY (RefundedBy) REFERENCES AspNetUsers(Id) ON DELETE SET NULL
    PRINT '✅ Created FK_OrganizerTicketPayments_AspNetUsers_RefundedBy'
END
ELSE
    PRINT '⚠️ FK_OrganizerTicketPayments_AspNetUsers_RefundedBy already exists'

PRINT 'Step 2 completed - All foreign keys added!'
