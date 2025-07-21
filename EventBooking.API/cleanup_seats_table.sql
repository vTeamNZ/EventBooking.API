-- Cleanup script to delete seats for all events except specified ones
-- This will help improve database performance by removing unnecessary seat records

-- First, let's check how many seats we're about to delete
SELECT 
    EventId,
    COUNT(*) as SeatCount
FROM Seats 
WHERE EventId NOT IN (4, 6, 19, 21)
GROUP BY EventId
ORDER BY EventId;

-- Show total count of seats to be deleted
SELECT COUNT(*) as TotalSeatsToDelete
FROM Seats 
WHERE EventId NOT IN (4, 6, 19, 21);

-- Show seats that will be kept
SELECT 
    EventId,
    COUNT(*) as SeatCount
FROM Seats 
WHERE EventId IN (4, 6, 19, 21)
GROUP BY EventId
ORDER BY EventId;

-- CAUTION: The following DELETE statement will permanently remove data
-- Make sure to backup your database before running this!

-- Delete all seats for events NOT in the keep list
DELETE FROM Seats 
WHERE EventId NOT IN (4, 6, 19, 21);

-- Verify the cleanup
SELECT 
    EventId,
    COUNT(*) as RemainingSeats
FROM Seats 
GROUP BY EventId
ORDER BY EventId;

-- Optional: Update statistics to improve query performance
-- (SQL Server specific - adjust for your database type)
-- UPDATE STATISTICS Seats;
