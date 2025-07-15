-- Fix remaining NULL TicketTypeId values

-- First check what events have seats but no ticket types
SELECT DISTINCT s.EventId, COUNT(*) as NullSeats
FROM Seats s
LEFT JOIN TicketTypes tt ON s.EventId = tt.EventId
WHERE s.TicketTypeId IS NULL
GROUP BY s.EventId;

-- Create default ticket types for events that don't have any
INSERT INTO TicketTypes (Type, Name, Color, Price, EventId, Description)
SELECT DISTINCT 
    'General Admission',
    'General Admission',
    '#007bff',
    50.00,
    s.EventId,
    'Default ticket type for event'
FROM Seats s
LEFT JOIN TicketTypes tt ON s.EventId = tt.EventId
WHERE s.TicketTypeId IS NULL
AND tt.Id IS NULL;

-- Now update the remaining NULL seats
UPDATE s
SET s.TicketTypeId = (
    SELECT TOP 1 tt.Id 
    FROM TicketTypes tt 
    WHERE tt.EventId = s.EventId
    ORDER BY tt.Id
)
FROM Seats s
WHERE s.TicketTypeId IS NULL;

-- Check if we still have NULL values
SELECT COUNT(*) AS SeatsWithNullTicketTypeId FROM Seats WHERE TicketTypeId IS NULL;
