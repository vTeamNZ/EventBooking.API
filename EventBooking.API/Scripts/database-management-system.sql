-- KiwiLanka Database Management System
-- This script provides utilities for smooth database operations

USE kwdb01;
GO

-- =============================================
-- 1. SMART SCHEMA VALIDATION FUNCTION
-- =============================================
CREATE OR ALTER PROCEDURE sp_ValidateSchema
AS
BEGIN
    PRINT '=== KiwiLanka Schema Validation Report ===';
    
    -- Check critical columns
    DECLARE @Issues TABLE (TableName NVARCHAR(50), Issue NVARCHAR(200), Severity NVARCHAR(10));
    
    -- Check Events.Title vs Events.Name
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Name')
        AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Title')
    BEGIN
        INSERT INTO @Issues VALUES ('Events', 'Has Name column but missing Title column', 'ERROR');
    END
    
    -- Check TicketTypes.Name
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
    BEGIN
        INSERT INTO @Issues VALUES ('TicketTypes', 'Missing Name column', 'ERROR');
    END
    
    -- Check Seats.TicketTypeId
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
    BEGIN
        INSERT INTO @Issues VALUES ('Seats', 'Missing TicketTypeId column', 'ERROR');
    END
    
    -- Check if old SectionId still exists
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
    BEGIN
        INSERT INTO @Issues VALUES ('Seats', 'Still has deprecated SectionId column', 'WARNING');
    END
    
    -- Show results
    IF EXISTS (SELECT * FROM @Issues WHERE Severity = 'ERROR')
    BEGIN
        PRINT 'ERRORS FOUND:';
        SELECT * FROM @Issues WHERE Severity = 'ERROR';
        RETURN 1; -- Error exit code
    END
    ELSE IF EXISTS (SELECT * FROM @Issues WHERE Severity = 'WARNING')
    BEGIN
        PRINT 'WARNINGS FOUND:';
        SELECT * FROM @Issues WHERE Severity = 'WARNING';
        RETURN 2; -- Warning exit code
    END
    ELSE
    BEGIN
        PRINT 'Schema validation PASSED - All good!';
        RETURN 0; -- Success
    END
END
GO

-- =============================================
-- 2. AUTOMATED SCHEMA FIX PROCEDURE
-- =============================================
CREATE OR ALTER PROCEDURE sp_FixSchema
AS
BEGIN
    PRINT '=== Auto-fixing schema issues ===';
    
    -- Fix Events Name->Title
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Name')
        AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Title')
    BEGIN
        EXEC sp_rename 'Events.Name', 'Title', 'COLUMN';
        PRINT '✓ Fixed: Renamed Events.Name to Title';
    END
    
    -- Add TicketTypes.Name
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
    BEGIN
        ALTER TABLE TicketTypes ADD Name nvarchar(100) NULL;
        PRINT '✓ Fixed: Added TicketTypes.Name column';
    END
    
    -- Add Seats.TicketTypeId
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
    BEGIN
        ALTER TABLE Seats ADD TicketTypeId int NOT NULL DEFAULT 1;
        PRINT '✓ Fixed: Added Seats.TicketTypeId column';
    END
    
    -- Remove old SectionId
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
    BEGIN
        -- Drop FK constraint first
        DECLARE @constraintName NVARCHAR(255);
        SELECT @constraintName = CONSTRAINT_NAME 
        FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
        WHERE CONSTRAINT_NAME LIKE '%Seats_Section%';
        
        IF @constraintName IS NOT NULL
            EXEC('ALTER TABLE Seats DROP CONSTRAINT ' + @constraintName);
            
        ALTER TABLE Seats DROP COLUMN SectionId;
        PRINT '✓ Fixed: Removed deprecated SectionId column';
    END
    
    PRINT '=== Schema fixes completed ===';
END
GO

-- =============================================
-- 3. SMART DATA SEEDING PROCEDURE
-- =============================================
CREATE OR ALTER PROCEDURE sp_SeedSampleData
    @ClearExisting BIT = 1,
    @DataLevel VARCHAR(10) = 'FULL' -- 'MINIMAL', 'BASIC', 'FULL'
AS
BEGIN
    PRINT '=== Seeding sample data (Level: ' + @DataLevel + ') ===';
    
    -- Clear existing data if requested
    IF @ClearExisting = 1
    BEGIN
        PRINT 'Clearing existing data...';
        DELETE FROM BookingFoods;
        DELETE FROM BookingTickets; 
        DELETE FROM Bookings;
        DELETE FROM SeatReservations;
        DELETE FROM Reservations;
        DELETE FROM Seats;
        DELETE FROM FoodItems;
        DELETE FROM TicketTypes;
        DELETE FROM Events;
        
        -- Don't clear Venues and Organizers in MINIMAL mode
        IF @DataLevel != 'MINIMAL'
        BEGIN
            DELETE FROM Venues;
            DELETE FROM Organizers;
        END
    END
    
    -- Seed based on level
    IF @DataLevel = 'MINIMAL'
    BEGIN
        -- Just 1 event for testing
        IF NOT EXISTS (SELECT * FROM Events WHERE Title = 'Test Event')
        BEGIN
            INSERT INTO Events (Title, Description, Date, Location, Price, Capacity, OrganizerId, VenueId, ImageUrl, IsActive, SeatSelectionMode)
            VALUES ('Test Event', 'Minimal test event', DATEADD(DAY, 30, GETDATE()), 'Test Location', 50.00, 100, 
                   (SELECT TOP 1 Id FROM Organizers), (SELECT TOP 1 Id FROM Venues), 'test.jpg', 1, 3);
                   
            INSERT INTO TicketTypes (Type, Name, Price, Description, EventId, Color)
            VALUES ('Standard', 'Standard Ticket', 50.00, 'Test ticket', 
                   (SELECT Id FROM Events WHERE Title = 'Test Event'), '#007bff');
        END
        PRINT '✓ MINIMAL data seeded';
    END
    ELSE IF @DataLevel = 'BASIC'
    BEGIN
        EXEC sp_SeedBasicData;
        PRINT '✓ BASIC data seeded';
    END
    ELSE -- FULL
    BEGIN
        EXEC sp_SeedFullData;
        PRINT '✓ FULL data seeded';
    END
END
GO

-- =============================================
-- 4. HEALTH CHECK PROCEDURE
-- =============================================
CREATE OR ALTER PROCEDURE sp_HealthCheck
AS
BEGIN
    PRINT '=== KiwiLanka Database Health Check ===';
    
    -- Check data counts
    SELECT 
        'Users' as Component, COUNT(*) as Count, 
        CASE WHEN COUNT(*) > 0 THEN '✓ OK' ELSE '✗ EMPTY' END as Status
    FROM AspNetUsers
    UNION ALL
    SELECT 'Organizers', COUNT(*), CASE WHEN COUNT(*) > 0 THEN '✓ OK' ELSE '✗ EMPTY' END FROM Organizers
    UNION ALL  
    SELECT 'Venues', COUNT(*), CASE WHEN COUNT(*) > 0 THEN '✓ OK' ELSE '✗ EMPTY' END FROM Venues
    UNION ALL
    SELECT 'Events', COUNT(*), CASE WHEN COUNT(*) > 0 THEN '✓ OK' ELSE '✗ EMPTY' END FROM Events
    UNION ALL
    SELECT 'TicketTypes', COUNT(*), CASE WHEN COUNT(*) > 0 THEN '✓ OK' ELSE '✗ EMPTY' END FROM TicketTypes
    UNION ALL
    SELECT 'FoodItems', COUNT(*), CASE WHEN COUNT(*) > 0 THEN '✓ OK' ELSE '✗ EMPTY' END FROM FoodItems;
    
    -- Check schema
    EXEC sp_ValidateSchema;
    
    PRINT '=== Health check completed ===';
END
GO

-- =============================================
-- 5. ONE-COMMAND SETUP PROCEDURE
-- =============================================
CREATE OR ALTER PROCEDURE sp_SetupDatabase
    @Mode VARCHAR(10) = 'QUICK' -- 'QUICK', 'FULL', 'RESET'
AS
BEGIN
    PRINT '=== KiwiLanka Database Setup (Mode: ' + @Mode + ') ===';
    
    -- Step 1: Fix schema
    EXEC sp_FixSchema;
    
    -- Step 2: Seed data based on mode
    IF @Mode = 'QUICK'
    BEGIN
        EXEC sp_SeedSampleData @ClearExisting = 0, @DataLevel = 'MINIMAL';
    END
    ELSE IF @Mode = 'FULL'
    BEGIN
        EXEC sp_SeedSampleData @ClearExisting = 1, @DataLevel = 'FULL';
    END
    ELSE IF @Mode = 'RESET'
    BEGIN
        EXEC sp_SeedSampleData @ClearExisting = 1, @DataLevel = 'BASIC';
    END
    
    -- Step 3: Health check
    EXEC sp_HealthCheck;
    
    PRINT '=== Setup completed! Test: https://kiwilanka.co.nz/api/Events ===';
END
GO

PRINT '=== KiwiLanka Database Management System installed! ===';
PRINT '';
PRINT 'Available commands:';
PRINT '• EXEC sp_HealthCheck                    -- Check database status';
PRINT '• EXEC sp_ValidateSchema                 -- Check schema issues';
PRINT '• EXEC sp_FixSchema                      -- Auto-fix schema problems';
PRINT '• EXEC sp_SetupDatabase ''QUICK''        -- Quick setup with minimal data';
PRINT '• EXEC sp_SetupDatabase ''FULL''         -- Full setup with all sample data';
PRINT '• EXEC sp_SetupDatabase ''RESET''        -- Reset and rebuild database';
PRINT '';
PRINT 'Usage: Just run "EXEC sp_SetupDatabase ''QUICK''" to fix everything!';
