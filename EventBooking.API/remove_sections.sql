-- Remove Section-related columns and tables

-- Drop foreign key constraints
DECLARE @constraint_name NVARCHAR(128);

-- Drop constraints from Seats table
SELECT @constraint_name = tc.CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY' 
AND tc.TABLE_NAME = 'Seats'
AND kcu.COLUMN_NAME = 'SectionId';

IF @constraint_name IS NOT NULL
BEGIN
    DECLARE @sql NVARCHAR(MAX) = 'ALTER TABLE Seats DROP CONSTRAINT ' + QUOTENAME(@constraint_name);
    EXEC sp_executesql @sql;
    PRINT 'Dropped foreign key constraint ' + @constraint_name + ' from Seats table';
END
ELSE
BEGIN
    PRINT 'No foreign key constraint found in Seats table';
END

-- Drop constraints from Tables table
SELECT @constraint_name = tc.CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
WHERE tc.CONSTRAINT_TYPE = 'FOREIGN KEY' 
AND tc.TABLE_NAME = 'Tables'
AND kcu.COLUMN_NAME = 'SectionId';

IF @constraint_name IS NOT NULL
BEGIN
    SET @sql = 'ALTER TABLE Tables DROP CONSTRAINT ' + QUOTENAME(@constraint_name);
    EXEC sp_executesql @sql;
    PRINT 'Dropped foreign key constraint ' + @constraint_name + ' from Tables table';
END
ELSE
BEGIN
    PRINT 'No foreign key constraint found in Tables table';
END

-- Drop index and SectionId column from Seats if they exist
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_SectionId' AND object_id = OBJECT_ID('Seats'))
BEGIN
    DROP INDEX IX_Seats_SectionId ON Seats;
    PRINT 'Dropped index IX_Seats_SectionId';
END

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
BEGIN
    ALTER TABLE Seats DROP COLUMN SectionId;
    PRINT 'Dropped SectionId column from Seats table';
END
ELSE
BEGIN
    PRINT 'SectionId column does not exist in Seats table';
END

-- Drop index and SectionId column from Tables if they exist
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tables_SectionId' AND object_id = OBJECT_ID('Tables'))
BEGIN
    DROP INDEX IX_Tables_SectionId ON Tables;
    PRINT 'Dropped index IX_Tables_SectionId';
END

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
BEGIN
    ALTER TABLE Tables DROP COLUMN SectionId;
    PRINT 'Dropped SectionId column from Tables table';
END
ELSE
BEGIN
    PRINT 'SectionId column does not exist in Tables table';
END

-- Drop Sections table if it exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    DROP TABLE Sections;
    PRINT 'Dropped Sections table';
END
ELSE
BEGIN
    PRINT 'Sections table does not exist';
END

PRINT 'Section removal completed';
