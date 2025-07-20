-- Production Database Verification Script for kwdb01
-- This script verifies all required changes have been applied
-- Date: July 20, 2025

PRINT '=== EventBooking.API Production Database Verification ===';
PRINT 'Database: kwdb01';
PRINT 'Date: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '';

-- 1. Check database connection and basic info
PRINT '1. Database Information:';
SELECT 
    DB_NAME() as DatabaseName,
    @@SERVERNAME as ServerName,
    GETDATE() as CurrentTime;

-- 2. Verify all required tables exist
PRINT '';
PRINT '2. Required Tables Check:';
SELECT 
    'Events' as TableName,
    CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Events') 
         THEN 'EXISTS' ELSE 'MISSING' END as Status
UNION ALL
SELECT 'Bookings', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Bookings') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'BookingLineItems', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingLineItems') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'TicketTypes', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TicketTypes') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'Seats', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Seats') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'Venues', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Venues') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'Organizers', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Organizers') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'SeatReservations', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SeatReservations') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'AspNetUsers', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetUsers') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'AspNetRoles', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AspNetRoles') THEN 'EXISTS' ELSE 'MISSING' END;

-- 3. Verify Events table has all required columns
PRINT '';
PRINT '3. Events Table Column Verification:';
SELECT 
    'ProcessingFeeEnabled' as ColumnName,
    CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeeEnabled') 
         THEN 'EXISTS' ELSE 'MISSING' END as Status
UNION ALL
SELECT 'ProcessingFeePercentage', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeePercentage') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'ProcessingFeeFixedAmount', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeeFixedAmount') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'Status', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Status') THEN 'EXISTS' ELSE 'MISSING' END
UNION ALL
SELECT 'SeatSelectionMode', CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'SeatSelectionMode') THEN 'EXISTS' ELSE 'MISSING' END;

-- 4. Verify Bookings table has ProcessingFee column
PRINT '';
PRINT '4. Bookings Table Column Verification:';
SELECT 
    'ProcessingFee' as ColumnName,
    CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'ProcessingFee') 
         THEN 'EXISTS' ELSE 'MISSING' END as Status;

-- 5. Verify TicketTypes table structure
PRINT '';
PRINT '5. TicketTypes Table Structure:';
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CASE 
        WHEN DATA_TYPE IN ('nvarchar', 'varchar') AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL 
        THEN CONCAT('(', CHARACTER_MAXIMUM_LENGTH, ')')
        WHEN DATA_TYPE IN ('decimal', 'numeric') 
        THEN CONCAT('(', NUMERIC_PRECISION, ',', NUMERIC_SCALE, ')')
        ELSE ''
    END as TypeDetails,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TicketTypes'
ORDER BY ORDINAL_POSITION;

-- 6. Check for Sections table (should be removed)
PRINT '';
PRINT '6. Deprecated Tables Check:';
SELECT 
    'Sections' as TableName,
    CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Sections') 
         THEN 'EXISTS (Should be removed)' ELSE 'REMOVED (Correct)' END as Status;

-- 7. Verify foreign key constraints
PRINT '';
PRINT '7. Important Foreign Key Constraints:';
SELECT 
    fk.name as ConstraintName,
    tp.name as ParentTable,
    cp.name as ParentColumn,
    tr.name as ReferencedTable,
    cr.name as ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.tables tp ON fk.parent_object_id = tp.object_id
INNER JOIN sys.tables tr ON fk.referenced_object_id = tr.object_id
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns cp ON fkc.parent_column_id = cp.column_id AND fkc.parent_object_id = cp.object_id
INNER JOIN sys.columns cr ON fkc.referenced_column_id = cr.column_id AND fkc.referenced_object_id = cr.object_id
WHERE fk.name IN (
    'FK_Seats_TicketTypes_TicketTypeId',
    'FK_Events_Venues_VenueId',
    'FK_Events_Organizers_OrganizerId',
    'FK_TicketTypes_Events_EventId'
)
ORDER BY fk.name;

-- 8. Check migration history
PRINT '';
PRINT '8. Recent Migration History:';
SELECT TOP 5
    MigrationId,
    ProductVersion
FROM __EFMigrationsHistory
ORDER BY MigrationId DESC;

-- 9. Verify user roles exist
PRINT '';
PRINT '9. User Roles Verification:';
SELECT 
    Name as RoleName,
    NormalizedName
FROM AspNetRoles
WHERE NormalizedName IN ('ADMIN', 'USER', 'ORGANIZER');

-- 10. Sample data counts
PRINT '';
PRINT '10. Data Counts:';
SELECT 'Events' as TableName, COUNT(*) as RecordCount FROM Events
UNION ALL
SELECT 'Organizers', COUNT(*) FROM Organizers
UNION ALL
SELECT 'Venues', COUNT(*) FROM Venues  
UNION ALL
SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL
SELECT 'Bookings', COUNT(*) FROM Bookings
UNION ALL
SELECT 'BookingLineItems', COUNT(*) FROM BookingLineItems
UNION ALL
SELECT 'AspNetUsers', COUNT(*) FROM AspNetUsers
UNION ALL
SELECT 'AspNetRoles', COUNT(*) FROM AspNetRoles;

PRINT '';
PRINT '=== Verification Complete ===';
PRINT 'Review the above results to ensure all required components are in place.';
PRINT 'If any items show MISSING status, run the migration script again.';
