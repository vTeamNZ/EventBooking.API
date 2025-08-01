-- Script to get list of users who have purchased tickets from kwdb01 production database
-- Connection: Server=tcp:kwsqlsvr01.database.windows.net,1433;Initial Catalog=kwdb01;User ID=gayantd;Password=maGulak@143456;

USE kwdb01;
GO

PRINT 'Connecting to kwdb01 production database...';
PRINT 'Getting list of users who have purchased tickets for Event ID 19...';
PRINT '';

-- Check if we're using the new BookingLineItems architecture or old structure
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingLineItems')
BEGIN
    PRINT 'Using NEW BookingLineItems architecture:';
    PRINT '================================================';
    
    -- Query using new BookingLineItems structure
    SELECT DISTINCT
        b.CustomerEmail as Email,
        b.CustomerFirstName as FirstName,
        b.CustomerLastName as LastName,
        b.CustomerMobile as Mobile,
        COUNT(DISTINCT b.Id) as TotalBookings,
        SUM(CASE WHEN bli.ItemType = 'Ticket' THEN bli.Quantity ELSE 0 END) as TotalTicketsPurchased,
        SUM(b.TotalAmount) as TotalSpent,
        MIN(b.CreatedAt) as FirstPurchaseDate,
        MAX(b.CreatedAt) as LastPurchaseDate,
        b.PaymentStatus
    FROM Bookings b
    INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
    WHERE b.EventId = 19  -- Filter for Event ID 19
      AND bli.ItemType = 'Ticket' 
      AND b.Status = 'Active'
      AND b.PaymentStatus IN ('Completed', 'succeeded')
    GROUP BY b.CustomerEmail, b.CustomerFirstName, b.CustomerLastName, b.CustomerMobile, b.PaymentStatus
    ORDER BY TotalTicketsPurchased DESC, LastPurchaseDate DESC;

    PRINT '';
    PRINT 'Summary Statistics:';
    PRINT '==================';
    
    SELECT 
        COUNT(DISTINCT b.CustomerEmail) as UniqueCustomers,
        SUM(CASE WHEN bli.ItemType = 'Ticket' THEN bli.Quantity ELSE 0 END) as TotalTicketsSold,
        COUNT(DISTINCT b.Id) as TotalBookings,
        SUM(b.TotalAmount) as TotalRevenue
    FROM Bookings b
    INNER JOIN BookingLineItems bli ON b.Id = bli.BookingId
    WHERE b.EventId = 19  -- Filter for Event ID 19
      AND bli.ItemType = 'Ticket' 
      AND b.Status = 'Active'
      AND b.PaymentStatus IN ('Completed', 'succeeded');
END
ELSE
BEGIN
    PRINT 'Using LEGACY BookingTickets architecture:';
    PRINT '==========================================';
    
    -- Query using legacy structure with Bookings + BookingTickets
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingTickets')
    BEGIN
        -- Try to get customer info from Payments table linked to Bookings
        SELECT DISTINCT
            COALESCE(p.Email, 'unknown@example.com') as Email,
            COALESCE(p.FirstName, 'Unknown') as FirstName,
            COALESCE(p.LastName, 'Customer') as LastName,
            p.Mobile,
            COUNT(DISTINCT b.Id) as TotalBookings,
            SUM(bt.Quantity) as TotalTicketsPurchased,
            SUM(b.TotalAmount) as TotalSpent,
            MIN(b.CreatedAt) as FirstPurchaseDate,
            MAX(b.CreatedAt) as LastPurchaseDate,
            p.Status as PaymentStatus
        FROM Bookings b
        INNER JOIN BookingTickets bt ON b.Id = bt.BookingId
        LEFT JOIN Payments p ON b.PaymentIntentId = p.PaymentIntentId
        WHERE b.EventId = 19  -- Filter for Event ID 19
          AND (p.Status IN ('succeeded', 'Completed') OR b.TotalAmount > 0)
        GROUP BY p.Email, p.FirstName, p.LastName, p.Mobile, p.Status
        ORDER BY TotalTicketsPurchased DESC, LastPurchaseDate DESC;

        PRINT '';
        PRINT 'Summary Statistics:';
        PRINT '==================';
        
        SELECT 
            COUNT(DISTINCT COALESCE(p.Email, b.PaymentIntentId)) as UniqueCustomers,
            SUM(bt.Quantity) as TotalTicketsSold,
            COUNT(DISTINCT b.Id) as TotalBookings,
            SUM(b.TotalAmount) as TotalRevenue
        FROM Bookings b
        INNER JOIN BookingTickets bt ON b.Id = bt.BookingId
        LEFT JOIN Payments p ON b.PaymentIntentId = p.PaymentIntentId
        WHERE b.EventId = 19  -- Filter for Event ID 19
          AND (p.Status IN ('succeeded', 'Completed') OR b.TotalAmount > 0);
    END
    ELSE
    BEGIN
        PRINT 'No BookingTickets table found. Checking EventBookings table...';
        
        -- Fallback to EventBookings if available
        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EventBookings')
        BEGIN
            SELECT DISTINCT
                BuyerEmail as Email,
                FirstName,
                'Unknown' as LastName,
                NULL as Mobile,
                COUNT(*) as TotalTicketsPurchased,
                MIN(CreatedAt) as FirstPurchaseDate,
                MAX(CreatedAt) as LastPurchaseDate
            FROM EventBookings
            WHERE EventID = '19'  -- Filter for Event ID 19 (stored as string in EventBookings)
              AND BuyerEmail IS NOT NULL 
              AND BuyerEmail != ''
            GROUP BY BuyerEmail, FirstName
            ORDER BY TotalTicketsPurchased DESC, LastPurchaseDate DESC;
        END
        ELSE
        BEGIN
            PRINT 'ERROR: No recognized booking tables found!';
            PRINT 'Available tables:';
            SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE' ORDER BY TABLE_NAME;
        END
    END
END

PRINT '';
PRINT 'Query completed successfully!';
