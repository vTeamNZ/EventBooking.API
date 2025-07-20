-- Quick fix: Update Events table to enable processing fees for existing events
-- This script ensures all events have processing fee settings based on appsettings.Production.json

PRINT 'Starting Events table processing fee update...';

-- Update existing events to enable processing fees with the configuration from appsettings.Production.json
-- 2.5% percentage with no fixed amount (using percentage-based calculation)
UPDATE Events 
SET 
    ProcessingFeeEnabled = 1,
    ProcessingFeePercentage = 2.5,
    ProcessingFeeFixedAmount = 0.0
WHERE 
    ProcessingFeeEnabled = 0 
    OR ProcessingFeePercentage = 0
    OR ProcessingFeePercentage IS NULL;

PRINT 'Updated Events with processing fee settings.';

-- Verify the current state
SELECT 
    Id,
    Title,
    Price,
    ProcessingFeeEnabled,
    ProcessingFeePercentage,
    ProcessingFeeFixedAmount,
    IsActive
FROM Events
WHERE IsActive = 1
ORDER BY Id;

PRINT 'Current active events with processing fee settings displayed.';

-- Check specific event ID 4 (Ladies Night from the error)
IF EXISTS (SELECT 1 FROM Events WHERE Id = 4)
BEGIN
    PRINT 'Event ID 4 details:';
    SELECT 
        Id,
        Title,
        Price,
        ProcessingFeeEnabled,
        ProcessingFeePercentage,
        ProcessingFeeFixedAmount,
        IsActive
    FROM Events 
    WHERE Id = 4;
END
ELSE
BEGIN
    PRINT 'Event ID 4 not found in database';
END

PRINT 'Events table processing fee update completed!';
