-- Check if BookingTickets table has any records at all
SELECT COUNT(*) as TotalBookingTickets FROM BookingTickets;

-- Check specific ticket type 34
SELECT * FROM BookingTickets WHERE TicketTypeId = 34;

-- Check all BookingTickets for event 18
SELECT bt.*, b.EventId, b.CreatedAt, b.TotalAmount 
FROM BookingTickets bt 
JOIN Bookings b ON bt.BookingId = b.Id 
WHERE b.EventId = 18;

-- Check ticket types for event 18
SELECT * FROM TicketTypes WHERE EventId = 18;

-- Check if there are any Bookings for event 18
SELECT * FROM Bookings WHERE EventId = 18;
