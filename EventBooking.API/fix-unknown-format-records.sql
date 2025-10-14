-- ========================================
-- Fix Unknown-Format Records in OrganizerTicketPayments
-- Extract correct SeatDetails from original BookingLineItems
-- ========================================

USE kwdb01;
GO

SET NOCOUNT ON;

PRINT '=== Fixing Unknown-Format Records ===';

-- Check current status
SELECT 
    EventId,
    COUNT(*) AS TotalRecords,
    COUNT(CASE WHEN SeatDetails = 'Unknown-Format' THEN 1 END) AS UnknownFormatCount
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%'
GROUP BY EventId
ORDER BY EventId;

PRINT '';
PRINT 'Processing Unknown-Format records...';

-- Update Unknown-Format records by extracting from original BookingLineItems
UPDATE otp
SET SeatDetails = 
    CASE 
        -- Extract ticket number from Notes (e.g., "Ticket #2 of 5")
        WHEN otp.Notes LIKE '%Ticket #1 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[0]')
        WHEN otp.Notes LIKE '%Ticket #2 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[1]')
        WHEN otp.Notes LIKE '%Ticket #3 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[2]')
        WHEN otp.Notes LIKE '%Ticket #4 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[3]')
        WHEN otp.Notes LIKE '%Ticket #5 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[4]')
        WHEN otp.Notes LIKE '%Ticket #6 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[5]')
        WHEN otp.Notes LIKE '%Ticket #7 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[6]')
        WHEN otp.Notes LIKE '%Ticket #8 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[7]')
        WHEN otp.Notes LIKE '%Ticket #9 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[8]')
        WHEN otp.Notes LIKE '%Ticket #10 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[9]')
        WHEN otp.Notes LIKE '%Ticket #11 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[10]')
        WHEN otp.Notes LIKE '%Ticket #12 of%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[11]')
        -- For "Single ticket" records, get the first (and only) ticket
        WHEN otp.Notes LIKE '%Single ticket%' 
        THEN JSON_VALUE(bli.SeatDetails, '$.allocatedTickets[0]')
        -- If no allocatedTickets, try seatNumber or other fields
        WHEN JSON_VALUE(bli.SeatDetails, '$.seatNumber') IS NOT NULL
        THEN JSON_VALUE(bli.SeatDetails, '$.seatNumber')
        ELSE 'Could-Not-Parse'
    END
FROM OrganizerTicketPayments otp
INNER JOIN BookingLineItems bli ON bli.Id = CAST(
    SUBSTRING(
        otp.Notes, 
        CHARINDEX('ID: ', otp.Notes) + 4, 
        CHARINDEX(' (', otp.Notes) - CHARINDEX('ID: ', otp.Notes) - 4
    ) AS INT
)
WHERE otp.SeatDetails = 'Unknown-Format'
  AND otp.Notes LIKE '%Migrated%';

PRINT 'Updated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' records';

-- Final verification
PRINT '';
PRINT '=== Final Status ===';
SELECT 
    EventId,
    COUNT(*) AS TotalRecords,
    COUNT(CASE WHEN SeatDetails = 'Unknown-Format' THEN 1 END) AS StillUnknownFormat,
    COUNT(CASE WHEN SeatDetails = 'Could-Not-Parse' THEN 1 END) AS CouldNotParse,
    COUNT(CASE WHEN SeatDetails NOT LIKE '%Format%' AND SeatDetails NOT LIKE '%Parse%' THEN 1 END) AS ProperlyFixed
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%'
GROUP BY EventId
ORDER BY EventId;

-- Show examples of fixed records
PRINT '';
PRINT 'Sample of fixed records:';
SELECT TOP 10
    otp.EventId,
    otp.Id,
    otp.SeatDetails,
    SUBSTRING(otp.Notes, 1, 50) + '...' AS NotesPreview
FROM OrganizerTicketPayments otp
WHERE otp.Notes LIKE '%Migrated%'
  AND otp.SeatDetails NOT LIKE '%Format%'
  AND otp.SeatDetails NOT LIKE '%Parse%'
  AND LEN(otp.SeatDetails) > 10  -- Recently fixed records
ORDER BY otp.Id DESC;

-- Show any remaining problematic records
IF EXISTS(SELECT 1 FROM OrganizerTicketPayments WHERE SeatDetails IN ('Unknown-Format', 'Could-Not-Parse'))
BEGIN
    PRINT '';
    PRINT 'Remaining problematic records:';
    SELECT TOP 5
        EventId,
        Id,
        SeatDetails,
        SUBSTRING(Notes, 1, 60) + '...' AS NotesPreview
    FROM OrganizerTicketPayments 
    WHERE SeatDetails IN ('Unknown-Format', 'Could-Not-Parse')
    ORDER BY EventId, Id;
END;

PRINT '';
PRINT '=== CLEANUP COMPLETE ===';