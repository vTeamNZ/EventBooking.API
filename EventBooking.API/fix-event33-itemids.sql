-- Fix ItemId = 0 for Event 33 organizer bookings
-- This script will update existing organizer booking line items to use proper ticket type IDs

USE kwdb01;
GO

PRINT 'Starting fix for Event 33 organizer booking ItemIds...';

-- First, let's see what we're working with
PRINT 'Current state - Organizer tickets with ItemId = 0 for Event 33:';
SELECT 
    bli.Id as LineItemId,
    b.Id as BookingId, 
    bli.ItemName,
    bli.ItemId,
    b.CreatedAt,
    b.CustomerEmail
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE b.EventId = 33 
    AND bli.ItemType = 'Ticket' 
    AND bli.ItemId = 0
    AND b.PaymentStatus = 'OrganizerDirect'
ORDER BY b.CreatedAt DESC;

-- Get available ticket types for Event 33
PRINT '';
PRINT 'Available ticket types for Event 33:';
SELECT Id, Type, Name, Price FROM TicketTypes WHERE EventId = 33;

-- Strategy: Update ItemId based on ticket type patterns in ItemName
-- Standing (16+ years) -> TicketTypeId = 75
-- Kids Corner (4-12 years) -> TicketTypeId = 82  
-- Child (13-15 years) -> TicketTypeId = 80

PRINT '';
PRINT 'Updating ItemIds for organizer bookings...';

-- Update Standing (16+ years) tickets
UPDATE bli
SET ItemId = 75
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE b.EventId = 33 
    AND bli.ItemType = 'Ticket' 
    AND bli.ItemId = 0
    AND b.PaymentStatus = 'OrganizerDirect'
    AND (bli.ItemName LIKE '%Standing%' OR bli.ItemName LIKE '%16+%' OR bli.ItemName LIKE '%Early B%');

PRINT CONCAT('Updated ', @@ROWCOUNT, ' Standing (16+) ticket records');

-- Update Kids Corner (4-12 years) tickets  
UPDATE bli
SET ItemId = 82
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE b.EventId = 33 
    AND bli.ItemType = 'Ticket' 
    AND bli.ItemId = 0
    AND b.PaymentStatus = 'OrganizerDirect'
    AND (bli.ItemName LIKE '%Kids%' OR bli.ItemName LIKE '%4-12%' OR bli.ItemName LIKE '%Corner%');

PRINT CONCAT('Updated ', @@ROWCOUNT, ' Kids Corner (4-12) ticket records');

-- Update Child (13-15 years) tickets
UPDATE bli
SET ItemId = 80
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE b.EventId = 33 
    AND bli.ItemType = 'Ticket' 
    AND bli.ItemId = 0
    AND b.PaymentStatus = 'OrganizerDirect'
    AND (bli.ItemName LIKE '%Child%' OR bli.ItemName LIKE '%13-15%');

PRINT CONCAT('Updated ', @@ROWCOUNT, ' Child (13-15) ticket records');

-- For any remaining tickets with ItemId = 0, default to Standing (most common)
UPDATE bli
SET ItemId = 75
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE b.EventId = 33 
    AND bli.ItemType = 'Ticket' 
    AND bli.ItemId = 0
    AND b.PaymentStatus = 'OrganizerDirect';

PRINT CONCAT('Updated ', @@ROWCOUNT, ' remaining tickets to Standing (default)');

PRINT '';
PRINT 'Verification - Check if any ItemId = 0 records remain for Event 33:';
SELECT COUNT(*) as RemainingZeroItemIds
FROM BookingLineItems bli
INNER JOIN Bookings b ON bli.BookingId = b.Id
WHERE b.EventId = 33 
    AND bli.ItemType = 'Ticket' 
    AND bli.ItemId = 0
    AND b.PaymentStatus = 'OrganizerDirect';

PRINT '';
PRINT 'Final state - Updated ticket distribution for Event 33:';
SELECT 
    tt.Id as TicketTypeId,
    tt.Type as TicketType,
    tt.Name as TicketName,
    COUNT(CASE WHEN bli.ItemId = tt.Id AND b.PaymentStatus != 'OrganizerDirect' THEN 1 END) as RegularTickets,
    COUNT(CASE WHEN bli.ItemId = tt.Id AND b.PaymentStatus = 'OrganizerDirect' THEN 1 END) as OrganizerTickets,
    COUNT(CASE WHEN bli.ItemId = tt.Id THEN 1 END) as TotalTickets
FROM TicketTypes tt
LEFT JOIN BookingLineItems bli ON tt.Id = bli.ItemId AND bli.ItemType = 'Ticket' AND bli.Status = 'Active'
LEFT JOIN Bookings b ON bli.BookingId = b.Id AND b.EventId = 33
WHERE tt.EventId = 33
GROUP BY tt.Id, tt.Type, tt.Name
ORDER BY tt.Id;

PRINT '';
PRINT 'Fix completed for Event 33!';
PRINT 'New availability calculations should now work correctly.';
