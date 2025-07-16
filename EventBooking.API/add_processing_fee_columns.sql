-- Add Processing Fee columns to Events table
-- Run this script manually in your database

-- Check if columns don't exist before adding them
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeePercentage')
BEGIN
    ALTER TABLE Events ADD ProcessingFeePercentage DECIMAL(5,4) NOT NULL DEFAULT 0.0000;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeeFixedAmount')
BEGIN
    ALTER TABLE Events ADD ProcessingFeeFixedAmount DECIMAL(18,2) NOT NULL DEFAULT 0.00;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeeEnabled')
BEGIN
    ALTER TABLE Events ADD ProcessingFeeEnabled BIT NOT NULL DEFAULT 0;
END

-- Verify the columns were added
SELECT 
    COLUMN_NAME, 
    DATA_TYPE, 
    NUMERIC_PRECISION, 
    NUMERIC_SCALE, 
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Events' 
    AND COLUMN_NAME IN ('ProcessingFeePercentage', 'ProcessingFeeFixedAmount', 'ProcessingFeeEnabled')
ORDER BY COLUMN_NAME;
