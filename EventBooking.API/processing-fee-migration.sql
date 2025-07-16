-- Processing Fee Database Migration Script
-- Run this on your production database to add processing fee functionality
-- Date: 2025-07-17

USE [kwdb01];  -- Adjust database name if different
GO

-- Check if columns already exist to prevent errors
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'ProcessingFeePercentage')
BEGIN
    ALTER TABLE [Events] ADD [ProcessingFeePercentage] decimal(5,4) NULL;
    PRINT 'Added ProcessingFeePercentage column to Events table';
END
ELSE
BEGIN
    PRINT 'ProcessingFeePercentage column already exists in Events table';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'ProcessingFeeFixedAmount')
BEGIN
    ALTER TABLE [Events] ADD [ProcessingFeeFixedAmount] decimal(18,2) NULL;
    PRINT 'Added ProcessingFeeFixedAmount column to Events table';
END
ELSE
BEGIN
    PRINT 'ProcessingFeeFixedAmount column already exists in Events table';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Events]') AND name = 'ProcessingFeeEnabled')
BEGIN
    ALTER TABLE [Events] ADD [ProcessingFeeEnabled] bit NOT NULL DEFAULT 0;
    PRINT 'Added ProcessingFeeEnabled column to Events table';
END
ELSE
BEGIN
    PRINT 'ProcessingFeeEnabled column already exists in Events table';
END

-- Verify the changes
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    IS_NULLABLE, 
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Events' 
    AND COLUMN_NAME IN ('ProcessingFeePercentage', 'ProcessingFeeFixedAmount', 'ProcessingFeeEnabled')
ORDER BY COLUMN_NAME;

PRINT 'Processing fee database migration completed successfully!';
GO
