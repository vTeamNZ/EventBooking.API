SELECT bt.*, b.EventId, b.CreatedAt, b.TotalAmount FROM BookingTickets bt JOIN Bookings b ON bt.BookingId = b.Id WHERE bt.TicketTypeId = 34 ORDER BY b.CreatedAt DESC;
