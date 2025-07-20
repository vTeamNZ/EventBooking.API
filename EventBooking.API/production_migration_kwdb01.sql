-- Production Migration Script for kwdb01
-- This script brings kwdb01 (production) up to date with kwdb02 (development)
-- Date: July 20, 2025
-- Description: Add missing ProcessingFee column to Bookings table and cleanup

PRINT 'Starting production migration for kwdb01...';

-- 1. Add ProcessingFee column to Bookings table if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'ProcessingFee')
BEGIN
    PRINT 'Adding ProcessingFee column to Bookings table...';
    ALTER TABLE [Bookings] ADD [ProcessingFee] DECIMAL(18,2) NOT NULL DEFAULT 0.00;
    PRINT 'ProcessingFee column added successfully.';
END
ELSE
BEGIN
    PRINT 'ProcessingFee column already exists in Bookings table.';
END

-- 2. Verify all processing fee related columns exist in Events table
PRINT 'Verifying Events table has all processing fee columns...';

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeePercentage')
BEGIN
    PRINT 'Adding ProcessingFeePercentage column to Events table...';
    ALTER TABLE [Events] ADD [ProcessingFeePercentage] DECIMAL(5,4) NOT NULL DEFAULT 0.0000;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeeFixedAmount')
BEGIN
    PRINT 'Adding ProcessingFeeFixedAmount column to Events table...';
    ALTER TABLE [Events] ADD [ProcessingFeeFixedAmount] DECIMAL(18,2) NOT NULL DEFAULT 0.00;
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'ProcessingFeeEnabled')
BEGIN
    PRINT 'Adding ProcessingFeeEnabled column to Events table...';
    ALTER TABLE [Events] ADD [ProcessingFeeEnabled] BIT NOT NULL DEFAULT 0;
END

-- 3. Verify Status column exists in Events table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Status')
BEGIN
    PRINT 'Adding Status column to Events table...';
    ALTER TABLE [Events] ADD [Status] INT NOT NULL DEFAULT 0;
    
    -- Update existing events to have Active status (2) if they are currently active, or Inactive (3) if not
    UPDATE [Events] 
    SET [Status] = CASE 
        WHEN [IsActive] = 1 THEN 2  -- Active
        ELSE 3                      -- Inactive
    END;
    PRINT 'Status column added and existing events updated.';
END

-- 4. Verify TicketTypes table has the correct structure
PRINT 'Verifying TicketTypes table structure...';

-- Check if Name column exists
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    PRINT 'Adding Name column to TicketTypes table...';
    ALTER TABLE [TicketTypes] ADD [Name] NVARCHAR(100) NULL;
END

-- Check if MaxTickets column exists
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'MaxTickets')
BEGIN
    PRINT 'Adding MaxTickets column to TicketTypes table...';
    ALTER TABLE [TicketTypes] ADD [MaxTickets] INT NULL;
END

-- 5. Update TicketTypes column constraints if needed
PRINT 'Updating TicketTypes column constraints...';

-- Check if Type column has proper length constraint
DECLARE @TypeColumnLength INT;
SELECT @TypeColumnLength = CHARACTER_MAXIMUM_LENGTH 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Type';

IF @TypeColumnLength IS NULL OR @TypeColumnLength > 50
BEGIN
    PRINT 'Updating Type column length constraint...';
    -- Note: This might fail if data exceeds 50 characters
    -- ALTER TABLE [TicketTypes] ALTER COLUMN [Type] NVARCHAR(50) NOT NULL;
    PRINT 'Type column constraint update skipped to avoid data truncation.';
END

-- Check if Color column has proper length constraint
DECLARE @ColorColumnLength INT;
SELECT @ColorColumnLength = CHARACTER_MAXIMUM_LENGTH 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color';

IF @ColorColumnLength IS NULL OR @ColorColumnLength > 7
BEGIN
    PRINT 'Color column may need length constraint update (manual review recommended).';
END

-- 6. Create BookingLineItems table if it doesn't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingLineItems')
BEGIN
    PRINT 'Creating BookingLineItems table...';
    CREATE TABLE [BookingLineItems] (
        [Id] INT IDENTITY(1,1) NOT NULL,
        [BookingId] INT NOT NULL,
        [ItemType] NVARCHAR(20) NOT NULL,
        [ItemId] INT NOT NULL,
        [ItemName] NVARCHAR(255) NOT NULL,
        [Quantity] INT NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [TotalPrice] DECIMAL(18,2) NOT NULL,
        [SeatDetails] NVARCHAR(MAX) NOT NULL,
        [ItemDetails] NVARCHAR(MAX) NOT NULL,
        [QRCode] NVARCHAR(500) NOT NULL,
        [Status] NVARCHAR(50) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        CONSTRAINT [PK_BookingLineItems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookingLineItems_Bookings_BookingId] 
            FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE CASCADE
    );
    
    -- Create index on BookingId for better performance
    CREATE INDEX [IX_BookingLineItems_BookingId] ON [BookingLineItems] ([BookingId]);
    
    PRINT 'BookingLineItems table created successfully.';
END
ELSE
BEGIN
    PRINT 'BookingLineItems table already exists.';
END

-- 7. Verify Seats table has TicketTypeId foreign key
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    PRINT 'Adding TicketTypeId column to Seats table...';
    ALTER TABLE [Seats] ADD [TicketTypeId] INT NOT NULL DEFAULT 1;
    
    -- Add foreign key constraint
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Seats_TicketTypes_TicketTypeId')
    BEGIN
        ALTER TABLE [Seats] ADD CONSTRAINT [FK_Seats_TicketTypes_TicketTypeId] 
        FOREIGN KEY ([TicketTypeId]) REFERENCES [TicketTypes] ([Id]);
        PRINT 'TicketTypeId foreign key constraint added to Seats table.';
    END
END

-- 8. Clean up Sections table if it exists and is empty
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Sections')
BEGIN
    DECLARE @SectionCount INT;
    SELECT @SectionCount = COUNT(*) FROM [Sections];
    
    IF @SectionCount = 0
    BEGIN
        PRINT 'Removing empty Sections table...';
        
        -- Drop foreign key constraints referencing Sections table
        IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Seats_Sections_SectionId')
        BEGIN
            ALTER TABLE [Seats] DROP CONSTRAINT [FK_Seats_Sections_SectionId];
        END
        
        IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Tables_Sections_SectionId')
        BEGIN
            ALTER TABLE [Tables] DROP CONSTRAINT [FK_Tables_Sections_SectionId];
        END
        
        -- Remove SectionId column from Seats if it exists
        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
        BEGIN
            ALTER TABLE [Seats] DROP COLUMN [SectionId];
        END
        
        -- Drop the Sections table
        DROP TABLE [Sections];
        PRINT 'Sections table and related constraints removed.';
    END
    ELSE
    BEGIN
        PRINT 'Sections table contains data - manual review required before removal.';
    END
END

-- 9. Verification queries
PRINT 'Running verification queries...';

PRINT 'Events table columns:';
SELECT 
    COLUMN_NAME, 
    DATA_TYPE,
    CASE 
        WHEN DATA_TYPE IN ('decimal', 'numeric') THEN CONCAT('(', NUMERIC_PRECISION, ',', NUMERIC_SCALE, ')')
        WHEN DATA_TYPE IN ('nvarchar', 'varchar') AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN CONCAT('(', CHARACTER_MAXIMUM_LENGTH, ')')
        ELSE ''
    END AS TYPE_DETAILS,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Events' 
    AND COLUMN_NAME IN ('ProcessingFeePercentage', 'ProcessingFeeFixedAmount', 'ProcessingFeeEnabled', 'Status')
ORDER BY COLUMN_NAME;

PRINT 'Bookings table ProcessingFee column:';
SELECT 
    COLUMN_NAME, 
    DATA_TYPE,
    CASE 
        WHEN DATA_TYPE IN ('decimal', 'numeric') THEN CONCAT('(', NUMERIC_PRECISION, ',', NUMERIC_SCALE, ')')
        ELSE ''
    END AS TYPE_DETAILS,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'Bookings' AND COLUMN_NAME = 'ProcessingFee';

PRINT 'TicketTypes table structure:';
SELECT 
    COLUMN_NAME, 
    DATA_TYPE,
    CASE 
        WHEN DATA_TYPE IN ('nvarchar', 'varchar') AND CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN CONCAT('(', CHARACTER_MAXIMUM_LENGTH, ')')
        ELSE ''
    END AS TYPE_DETAILS,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'TicketTypes'
ORDER BY ORDINAL_POSITION;

PRINT 'BookingLineItems table verification:';
SELECT 
    'BookingLineItems' as TableName,
    CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'BookingLineItems') 
         THEN 'EXISTS' ELSE 'MISSING' END as Status;

PRINT 'Migration completed successfully!';

-- 10. Update migration history
IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250720000000_ProductionMigrationKwdb01')
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES ('20250720000000_ProductionMigrationKwdb01', '8.0.16');
    PRINT 'Migration history updated.';
END

PRINT 'Production migration script completed.';
