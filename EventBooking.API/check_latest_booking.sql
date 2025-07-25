-- Check latest Bookings for event 18
SELECT TOP 5 * FROM Bookings WHERE EventId = 18 ORDER BY CreatedAt DESC;

-- Check BookingTickets for event 18 bookings
SELECT bt.*, b.EventId, b.CreatedAt, b.TotalAmount 
FROM BookingTickets bt 
JOIN Bookings b ON bt.BookingId = b.Id 
WHERE b.EventId = 18 
ORDER BY b.CreatedAt DESC;

-- Check specific ticket type 34 sales
SELECT COUNT(*) as TotalSales, SUM(Quantity) as TotalQuantity 
FROM BookingTickets 
WHERE TicketTypeId = 34;

-- Check ticket type 34 details
SELECT * FROM TicketTypes WHERE Id = 34;
