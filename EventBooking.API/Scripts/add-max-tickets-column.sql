-- Add MaxTickets column to TicketTypes table for General Admission capacity limits
-- This enables ticket type level capacity management for General Admission events

-- Check if MaxTickets column already exists
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'MaxTickets')
BEGIN
    -- Add MaxTickets column
    ALTER TABLE TicketTypes 
    ADD MaxTickets int NULL;
    
    PRINT '✓ Added MaxTickets column to TicketTypes table';
END
ELSE
BEGIN
    PRINT '✓ MaxTickets column already exists in TicketTypes table';
END

-- Use GO to separate batches so the new column is recognized
GO

-- Update existing General Admission events with sample MaxTickets values
-- This is for events that have SeatSelectionMode = 3 (GeneralAdmission)
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'MaxTickets')
BEGIN
    UPDATE tt 
    SET MaxTickets = CASE 
        WHEN tt.Type LIKE '%VIP%' THEN 50
        WHEN tt.Type LIKE '%Premium%' THEN 100  
        WHEN tt.Type LIKE '%Standard%' THEN 200
        WHEN tt.Type LIKE '%General%' THEN 300
        WHEN tt.Type LIKE '%Student%' THEN 100
        WHEN tt.Type LIKE '%All Access%' OR tt.Type LIKE '%Conference%' THEN 500
        ELSE 100 -- Default value
    END
    FROM TicketTypes tt
    INNER JOIN Events e ON tt.EventId = e.Id
    WHERE e.SeatSelectionMode = 3 -- General Admission
      AND tt.MaxTickets IS NULL;

    PRINT '✓ Updated existing General Admission ticket types with MaxTickets values';
END

-- For events with allocated seating (SeatSelectionMode = 1), MaxTickets should remain NULL
-- as capacity is determined by seat assignments
PRINT '✓ Script completed successfully';
