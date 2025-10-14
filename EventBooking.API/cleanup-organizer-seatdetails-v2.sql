-- ========================================
-- SIMPLIFIED OrganizerTicketPayments SeatDetails Cleanup Script v2
-- Uses SQL Server JSON functions for reliable parsing
-- ========================================

USE kwdb01;
GO

SET NOCOUNT ON;

PRINT '=== OrganizerTicketPayments SeatDetails Cleanup v2 ===';

-- First, let's see current status
SELECT 
    'Current Status' AS Analysis,
    CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END AS Format,
    COUNT(*) AS RecordCount
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%'
GROUP BY CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END;

PRINT '';

-- Get sample of records that still need processing
SELECT TOP 5
    Id,
    SUBSTRING(SeatDetails, 1, 100) + '...' AS SeatDetailsPreview,
    SUBSTRING(Notes, 1, 60) + '...' AS NotesPreview
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%' 
  AND SeatDetails LIKE '%{%'
ORDER BY Id;

PRINT '';
PRINT 'Processing JSON records using JSON_VALUE and string functions...';

-- Update records using JSON_VALUE and string parsing
UPDATE OrganizerTicketPayments
SET SeatDetails = 
    CASE 
        -- Extract from allocatedTickets array using ticket number from Notes
        WHEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets') IS NOT NULL 
        THEN (
            -- This will get the allocatedTickets array as string, we'll parse it manually
            CASE 
                WHEN CHARINDEX('Ticket #1 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[0]')
                WHEN CHARINDEX('Ticket #2 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[1]')
                WHEN CHARINDEX('Ticket #3 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[2]')
                WHEN CHARINDEX('Ticket #4 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[3]')
                WHEN CHARINDEX('Ticket #5 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[4]')
                WHEN CHARINDEX('Ticket #6 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[5]')
                WHEN CHARINDEX('Ticket #7 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[6]')
                WHEN CHARINDEX('Ticket #8 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[7]')
                WHEN CHARINDEX('Ticket #9 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[8]')
                WHEN CHARINDEX('Ticket #10 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[9]')
                WHEN CHARINDEX('Ticket #11 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[10]')
                WHEN CHARINDEX('Ticket #12 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedTickets[11]')
                ELSE 'Unknown-Ticket'
            END
        )
        -- Extract from allocatedSeats array if allocatedTickets is null
        WHEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats') IS NOT NULL 
        THEN (
            CASE 
                WHEN CHARINDEX('Ticket #1 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[0]')
                WHEN CHARINDEX('Ticket #2 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[1]')
                WHEN CHARINDEX('Ticket #3 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[2]')
                WHEN CHARINDEX('Ticket #4 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[3]')
                WHEN CHARINDEX('Ticket #5 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[4]')
                WHEN CHARINDEX('Ticket #6 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[5]')
                WHEN CHARINDEX('Ticket #7 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[6]')
                WHEN CHARINDEX('Ticket #8 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[7]')
                WHEN CHARINDEX('Ticket #9 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[8]')
                WHEN CHARINDEX('Ticket #10 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[9]')
                WHEN CHARINDEX('Ticket #11 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[10]')
                WHEN CHARINDEX('Ticket #12 of', Notes) > 0 
                THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.allocatedSeats[11]')
                ELSE 'Unknown-Seat'
            END
        )
        -- Extract from seatNumber if it's a simple JSON object
        WHEN JSON_VALUE(SeatDetails, '$.seatNumber') IS NOT NULL 
        THEN JSON_VALUE(SeatDetails, '$.seatNumber')
        -- Extract from originalSeatDetails.seatNumber
        WHEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.seatNumber') IS NOT NULL 
        THEN JSON_VALUE(SeatDetails, '$.originalSeatDetails.seatNumber')
        ELSE 'Unknown-Format'
    END
WHERE Notes LIKE '%Migrated%' 
  AND SeatDetails LIKE '%{%'
  AND ISJSON(SeatDetails) = 1;

PRINT 'Update completed. Rows affected: ' + CAST(@@ROWCOUNT AS VARCHAR(10));

-- Final verification
PRINT '';
PRINT '=== Final Status ===';
SELECT 
    'After Cleanup' AS Analysis,
    CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END AS Format,
    COUNT(*) AS RecordCount
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%'
GROUP BY CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END;

-- Show some examples of cleaned records
PRINT '';
PRINT 'Sample of cleaned records:';
SELECT TOP 10
    Id,
    TicketTypeId,
    SeatDetails,
    SUBSTRING(Notes, CHARINDEX('Ticket #', Notes), 15) AS TicketInfo
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%' 
  AND SeatDetails NOT LIKE '%{%'
  AND LEN(SeatDetails) < 50  -- Recently cleaned records should be short
ORDER BY Id DESC;

-- Show any remaining problematic records
IF EXISTS(SELECT 1 FROM OrganizerTicketPayments WHERE Notes LIKE '%Migrated%' AND SeatDetails LIKE '%{%')
BEGIN
    PRINT '';
    PRINT 'Remaining JSON records (may need manual review):';
    SELECT TOP 5
        Id,
        SUBSTRING(SeatDetails, 1, 80) + '...' AS SeatDetailsPreview,
        SUBSTRING(Notes, 1, 50) + '...' AS NotesPreview
    FROM OrganizerTicketPayments 
    WHERE Notes LIKE '%Migrated%' 
      AND SeatDetails LIKE '%{%'
    ORDER BY Id;
END;

PRINT '';
PRINT '=== CLEANUP COMPLETE ===';