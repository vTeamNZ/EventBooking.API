-- ========================================
-- OrganizerTicketPayments SeatDetails Cleanup Script
-- Extract specific ticket identifiers from JSON based on Notes column
-- ========================================
-- Problem: 161 migrated records have full JSON in SeatDetails
-- Solution: Parse Notes to get ticket number, extract specific ticket from JSON
-- Format: Notes = "Migrated from BookingLineItem ID: X (Ticket #Y of Z)"
-- Extract: The Y-th ticket from allocatedTickets or allocatedSeats array

USE kwdb01;
GO

SET NOCOUNT ON;

PRINT '=== OrganizerTicketPayments SeatDetails Cleanup ===';
PRINT 'Target: 161 JSON records that need to be converted to simple format';
PRINT '';

-- Step 1: Analyze current state
SELECT 
    'Current State Analysis' AS Analysis,
    CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END AS Format,
    COUNT(*) AS RecordCount
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%'
GROUP BY CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END;

PRINT '';
PRINT '=== Processing JSON Records ===';

-- Step 2: Create temporary table to store extraction results
IF OBJECT_ID('tempdb..#TicketExtractions') IS NOT NULL DROP TABLE #TicketExtractions;

CREATE TABLE #TicketExtractions (
    Id INT,
    TicketNumber INT,
    TotalTickets INT,
    BookingLineItemId INT,
    ExtractedTicket NVARCHAR(500),
    CurrentSeatDetails NVARCHAR(MAX),
    Notes NVARCHAR(MAX)
);

-- Step 3: Extract ticket numbers from Notes and process JSON
INSERT INTO #TicketExtractions (Id, TicketNumber, TotalTickets, BookingLineItemId, CurrentSeatDetails, Notes)
SELECT 
    Id,
    CAST(SUBSTRING(Notes, 
        CHARINDEX('Ticket #', Notes) + 8, 
        CHARINDEX(' of ', Notes) - CHARINDEX('Ticket #', Notes) - 8
    ) AS INT) AS TicketNumber,
    CAST(SUBSTRING(Notes, 
        CHARINDEX(' of ', Notes) + 4, 
        CHARINDEX(')', Notes) - CHARINDEX(' of ', Notes) - 4
    ) AS INT) AS TotalTickets,
    CAST(SUBSTRING(Notes, 
        CHARINDEX('ID: ', Notes) + 4, 
        CHARINDEX(' (Ticket', Notes) - CHARINDEX('ID: ', Notes) - 4
    ) AS INT) AS BookingLineItemId,
    SeatDetails,
    Notes
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%' 
  AND SeatDetails LIKE '%{%'
  AND Notes LIKE '%Ticket #%of%';

PRINT 'Extracted ticket information for ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' records';

-- Step 4: Process different JSON formats and extract the correct ticket
DECLARE @Id INT, @TicketNumber INT, @SeatDetails NVARCHAR(MAX), @ExtractedTicket NVARCHAR(500);
DECLARE @AllocatedTickets NVARCHAR(MAX), @AllocatedSeats NVARCHAR(MAX), @SeatNumber NVARCHAR(500);

DECLARE ticket_cursor CURSOR FOR
SELECT Id, TicketNumber, CurrentSeatDetails
FROM #TicketExtractions
ORDER BY Id;

OPEN ticket_cursor;
FETCH NEXT FROM ticket_cursor INTO @Id, @TicketNumber, @SeatDetails;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ExtractedTicket = NULL;
    
    -- Method 1: Extract from allocatedTickets array
    IF @SeatDetails LIKE '%"allocatedTickets":%' 
    BEGIN
        -- Extract the allocatedTickets array
        SET @AllocatedTickets = SUBSTRING(@SeatDetails, 
            CHARINDEX('"allocatedTickets":[', @SeatDetails) + 19,
            CHARINDEX(']', @SeatDetails, CHARINDEX('"allocatedTickets":[', @SeatDetails)) - CHARINDEX('"allocatedTickets":[', @SeatDetails) - 19
        );
        
        -- Split and get the nth ticket (1-based index)
        DECLARE @TicketArray TABLE (RowNum INT IDENTITY(1,1), TicketName NVARCHAR(500));
        
        -- Parse comma-separated tickets
        DECLARE @Ticket NVARCHAR(500), @Pos INT = 1, @NextPos INT;
        WHILE @Pos <= LEN(@AllocatedTickets)
        BEGIN
            SET @NextPos = CHARINDEX(',', @AllocatedTickets, @Pos);
            IF @NextPos = 0 SET @NextPos = LEN(@AllocatedTickets) + 1;
            
            SET @Ticket = LTRIM(RTRIM(SUBSTRING(@AllocatedTickets, @Pos, @NextPos - @Pos)));
            SET @Ticket = REPLACE(REPLACE(@Ticket, '"', ''), '''', ''); -- Remove quotes
            
            IF LEN(@Ticket) > 0
                INSERT INTO @TicketArray (TicketName) VALUES (@Ticket);
            
            SET @Pos = @NextPos + 1;
        END;
        
        -- Get the specific ticket for this row
        SELECT @ExtractedTicket = TicketName 
        FROM @TicketArray 
        WHERE RowNum = @TicketNumber;
        
        DELETE FROM @TicketArray;
    END
    
    -- Method 2: Extract from allocatedSeats array  
    ELSE IF @SeatDetails LIKE '%"allocatedSeats":%' AND @SeatDetails NOT LIKE '%"allocatedSeats":null%'
    BEGIN
        -- Extract the allocatedSeats array
        SET @AllocatedSeats = SUBSTRING(@SeatDetails, 
            CHARINDEX('"allocatedSeats":[', @SeatDetails) + 17,
            CHARINDEX(']', @SeatDetails, CHARINDEX('"allocatedSeats":[', @SeatDetails)) - CHARINDEX('"allocatedSeats":[', @SeatDetails) - 17
        );
        
        -- Split and get the nth seat (1-based index)
        DECLARE @SeatArray TABLE (RowNum INT IDENTITY(1,1), SeatName NVARCHAR(500));
        
        -- Parse comma-separated seats
        SET @Pos = 1;
        WHILE @Pos <= LEN(@AllocatedSeats)
        BEGIN
            SET @NextPos = CHARINDEX(',', @AllocatedSeats, @Pos);
            IF @NextPos = 0 SET @NextPos = LEN(@AllocatedSeats) + 1;
            
            SET @Ticket = LTRIM(RTRIM(SUBSTRING(@AllocatedSeats, @Pos, @NextPos - @Pos)));
            SET @Ticket = REPLACE(REPLACE(@Ticket, '"', ''), '''', ''); -- Remove quotes
            
            IF LEN(@Ticket) > 0
                INSERT INTO @SeatArray (SeatName) VALUES (@Ticket);
            
            SET @Pos = @NextPos + 1;
        END;
        
        -- Get the specific seat for this row
        SELECT @ExtractedTicket = SeatName 
        FROM @SeatArray 
        WHERE RowNum = @TicketNumber;
        
        DELETE FROM @SeatArray;
    END
    
    -- Method 3: Extract from seatNumber field (for simple JSON objects)
    ELSE IF @SeatDetails LIKE '%"seatNumber":%'
    BEGIN
        SET @SeatNumber = SUBSTRING(@SeatDetails, 
            CHARINDEX('"seatNumber":"', @SeatDetails) + 14,
            CHARINDEX('"', @SeatDetails, CHARINDEX('"seatNumber":"', @SeatDetails) + 14) - CHARINDEX('"seatNumber":"', @SeatDetails) - 14
        );
        SET @ExtractedTicket = @SeatNumber;
    END;
    
    -- Update the extraction result
    UPDATE #TicketExtractions 
    SET ExtractedTicket = @ExtractedTicket 
    WHERE Id = @Id;
    
    FETCH NEXT FROM ticket_cursor INTO @Id, @TicketNumber, @SeatDetails;
END;

CLOSE ticket_cursor;
DEALLOCATE ticket_cursor;

-- Step 5: Show extraction results for verification
SELECT 
    'Extraction Results' AS Summary,
    COUNT(*) AS TotalProcessed,
    COUNT(CASE WHEN ExtractedTicket IS NOT NULL THEN 1 END) AS SuccessfulExtractions,
    COUNT(CASE WHEN ExtractedTicket IS NULL THEN 1 END) AS FailedExtractions
FROM #TicketExtractions;

-- Show some examples
PRINT '';
PRINT 'Sample Extraction Results:';
SELECT TOP 10 
    Id, 
    TicketNumber, 
    TotalTickets,
    ExtractedTicket,
    LEFT(CurrentSeatDetails, 80) + '...' AS SeatDetailsPreview
FROM #TicketExtractions 
WHERE ExtractedTicket IS NOT NULL
ORDER BY Id;

-- Show failed extractions for debugging
IF EXISTS(SELECT 1 FROM #TicketExtractions WHERE ExtractedTicket IS NULL)
BEGIN
    PRINT '';
    PRINT 'Failed Extractions (need manual review):';
    SELECT 
        Id, 
        TicketNumber, 
        LEFT(CurrentSeatDetails, 100) + '...' AS SeatDetailsPreview,
        LEFT(Notes, 80) + '...' AS NotesPreview
    FROM #TicketExtractions 
    WHERE ExtractedTicket IS NULL
    ORDER BY Id;
END;

-- Step 6: Apply the updates to the actual table
PRINT '';
PRINT 'Applying updates to OrganizerTicketPayments table...';

DECLARE @UpdateCount INT = 0;

DECLARE update_cursor CURSOR FOR
SELECT Id, ExtractedTicket
FROM #TicketExtractions
WHERE ExtractedTicket IS NOT NULL;

OPEN update_cursor;
FETCH NEXT FROM update_cursor INTO @Id, @ExtractedTicket;

WHILE @@FETCH_STATUS = 0
BEGIN
    UPDATE OrganizerTicketPayments 
    SET SeatDetails = @ExtractedTicket
    WHERE Id = @Id;
    
    SET @UpdateCount = @UpdateCount + 1;
    
    FETCH NEXT FROM update_cursor INTO @Id, @ExtractedTicket;
END;

CLOSE update_cursor;
DEALLOCATE update_cursor;

PRINT 'Successfully updated ' + CAST(@UpdateCount AS VARCHAR(10)) + ' records';

-- Step 7: Final verification
PRINT '';
PRINT '=== Final State Analysis ===';
SELECT 
    'After Cleanup' AS Analysis,
    CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END AS Format,
    COUNT(*) AS RecordCount
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%'
GROUP BY CASE WHEN SeatDetails LIKE '%{%' THEN 'JSON Format' ELSE 'Simple Format' END;

-- Show sample of cleaned records
PRINT '';
PRINT 'Sample of cleaned records:';
SELECT TOP 10
    Id,
    TicketTypeId,
    SeatDetails,
    LEFT(Notes, 60) + '...' AS NotesPreview
FROM OrganizerTicketPayments 
WHERE Notes LIKE '%Migrated%' 
  AND SeatDetails NOT LIKE '%{%'
  AND Id IN (SELECT Id FROM #TicketExtractions)
ORDER BY Id;

-- Cleanup
DROP TABLE #TicketExtractions;

PRINT '';
PRINT '=== CLEANUP COMPLETE ===';
PRINT 'All migrated OrganizerTicketPayments records should now have clean SeatDetails';
PRINT 'containing only the specific seat/ticket identifier for each row.';