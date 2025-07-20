-- Quick diagnosis: Check Events table structure and data for Event ID 4
PRINT 'Checking Events table structure...';

-- Check if processing fee columns exist
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Events' 
AND COLUMN_NAME LIKE '%Processing%'
ORDER BY ORDINAL_POSITION;

PRINT 'Processing fee columns in Events table displayed.';

-- Check Event ID 4 specifically (the one failing in the error)
IF EXISTS (SELECT 1 FROM Events WHERE Id = 4)
BEGIN
    PRINT 'Event ID 4 data:';
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
    PRINT 'Event ID 4 not found. Showing all events:';
    SELECT 
        Id,
        Title,
        Price,
        CASE 
            WHEN TRY_CAST(ProcessingFeeEnabled AS BIT) IS NULL THEN 'Column Missing'
            ELSE CAST(ProcessingFeeEnabled AS VARCHAR(10))
        END as ProcessingFeeEnabled,
        CASE 
            WHEN TRY_CAST(ProcessingFeePercentage AS DECIMAL) IS NULL THEN 'Column Missing'
            ELSE CAST(ProcessingFeePercentage AS VARCHAR(10))
        END as ProcessingFeePercentage,
        IsActive
    FROM Events 
    WHERE IsActive = 1
    ORDER BY Id;
END
