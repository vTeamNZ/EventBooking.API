-- Add processing fee columns to Events table if they don't exist
-- This script is safe to run multiple times

-- Check if ProcessingFeePercentage column exists
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('Events') 
    AND name = 'ProcessingFeePercentage'
)
BEGIN
    ALTER TABLE Events ADD ProcessingFeePercentage decimal(5,2) NOT NULL DEFAULT 0.0
    PRINT 'Added ProcessingFeePercentage column to Events table'
END
ELSE
BEGIN
    PRINT 'ProcessingFeePercentage column already exists in Events table'
END

-- Check if ProcessingFeeFixedAmount column exists
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('Events') 
    AND name = 'ProcessingFeeFixedAmount'
)
BEGIN
    ALTER TABLE Events ADD ProcessingFeeFixedAmount decimal(10,2) NOT NULL DEFAULT 0.0
    PRINT 'Added ProcessingFeeFixedAmount column to Events table'
END
ELSE
BEGIN
    PRINT 'ProcessingFeeFixedAmount column already exists in Events table'
END

-- Check if ProcessingFeeEnabled column exists
IF NOT EXISTS (
    SELECT * FROM sys.columns 
    WHERE object_id = OBJECT_ID('Events') 
    AND name = 'ProcessingFeeEnabled'
)
BEGIN
    ALTER TABLE Events ADD ProcessingFeeEnabled bit NOT NULL DEFAULT 0
    PRINT 'Added ProcessingFeeEnabled column to Events table'
END
ELSE
BEGIN
    PRINT 'ProcessingFeeEnabled column already exists in Events table'
END

-- Update existing events to have processing fee enabled by default
-- Based on the configuration from appsettings.Production.json (2.5% with $10 max)
UPDATE Events 
SET 
    ProcessingFeeEnabled = 1,
    ProcessingFeePercentage = 2.5,
    ProcessingFeeFixedAmount = 0.0
WHERE ProcessingFeeEnabled = 0 OR ProcessingFeePercentage = 0

PRINT 'Updated existing events with default processing fee settings'

-- Verify the changes
SELECT 
    Id,
    Title,
    ProcessingFeeEnabled,
    ProcessingFeePercentage,
    ProcessingFeeFixedAmount
FROM Events
ORDER BY Id

PRINT 'Processing fee columns added and configured successfully!'
