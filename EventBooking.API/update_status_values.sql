-- Update existing events with correct status values
UPDATE [Events] 
SET [Status] = CASE 
    WHEN [IsActive] = 1 THEN 2  -- Active
    ELSE 3                      -- Inactive
END;
