-- Add Status column to Events table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'Status')
BEGIN
    -- First add the column with default value
    ALTER TABLE [Events] ADD [Status] int NOT NULL DEFAULT 0;
    PRINT 'Status column added to Events table';
END
ELSE
BEGIN
    PRINT 'Status column already exists in Events table';
END

-- Update existing events to have Active status (2) if they are currently active, or Inactive (3) if not
-- Only do this if the column was just added
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'Status')
BEGIN
    UPDATE [Events] 
    SET [Status] = CASE 
        WHEN [IsActive] = 1 THEN 2  -- Active
        ELSE 3                      -- Inactive
    END;
    
    PRINT 'Existing events updated with appropriate status values';
END
