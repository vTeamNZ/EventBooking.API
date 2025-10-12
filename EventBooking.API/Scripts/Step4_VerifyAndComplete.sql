-- ================================================================
-- STEP 4: UPDATE MIGRATION HISTORY AND VERIFY
-- ================================================================

PRINT 'Updating migration history...'

-- Update migration history
IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId = '20251010050823_AddRefundCancellationSupport')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20251010050823_AddRefundCancellationSupport', '8.0.0')
    PRINT '✅ Added migration to history'
END
ELSE
    PRINT '⚠️ Migration already exists in history'

PRINT ''
PRINT '================================================================'
PRINT 'VERIFICATION - Checking all changes'
PRINT '================================================================'

-- Verify columns
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
    c.name AS ColumnName
FROM sys.foreign_keys fk
INNER JOIN sys.tables t ON fk.parent_object_id = t.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
WHERE fk.name LIKE '%RefundedBy%'
ORDER BY fk.name

-- Verify indexes
PRINT ''
PRINT 'Performance indexes:'
SELECT 
    i.name AS IndexName,
    t.name AS TableName
FROM sys.indexes i
INNER JOIN sys.tables t ON i.object_id = t.object_id
WHERE i.name LIKE '%Status%' OR i.name LIKE '%RefundedAt%' OR i.name LIKE '%BookingLineItemId%'
ORDER BY t.name, i.name

PRINT ''
PRINT '================================================================'
PRINT 'REFUND CANCELLATION ENHANCEMENT COMPLETED SUCCESSFULLY!'
PRINT '================================================================'
