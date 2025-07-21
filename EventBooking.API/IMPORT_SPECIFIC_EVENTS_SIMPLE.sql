-- Import specific events and all related data into kwdb01_local
-- Simple version with better error handling

PRINT '=== IMPORTING EVENTS 4, 6, 19, 21 AND ALL RELATED DATA ==='
PRINT ''

-- Step 1: Import AspNetUsers first (if file exists)
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'aspnetusers' AND type = 'U')
BEGIN
    PRINT 'Step 1: Importing AspNetUsers data...'
    BEGIN TRY
        BULK INSERT AspNetUsers
        FROM 'C:\temp\aspnetusers.dat'
        WITH (
            FIELDTERMINATOR = '|',
            ROWTERMINATOR = '\n',
            FIRSTROW = 1,
            KEEPIDENTITY
        );
        PRINT '  ✓ AspNetUsers imported successfully'
    END TRY
    BEGIN CATCH
        PRINT '  ⚠ AspNetUsers import failed or no data: ' + ERROR_MESSAGE()
    END CATCH
END

-- Step 2: Import Organizers
PRINT 'Step 2: Importing Organizers data...'
BEGIN TRY
    BULK INSERT Organizers
    FROM 'C:\temp\organizers.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ Organizers imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ Organizers import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 3: Import Events
PRINT 'Step 3: Importing Events data...'
BEGIN TRY
    BULK INSERT Events
    FROM 'C:\temp\events.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ Events imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ Events import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 4: Import TicketTypes
PRINT 'Step 4: Importing TicketTypes data...'
BEGIN TRY
    BULK INSERT TicketTypes
    FROM 'C:\temp\tickettypes.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ TicketTypes imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ TicketTypes import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 5: Import FoodItems
PRINT 'Step 5: Importing FoodItems data...'
BEGIN TRY
    BULK INSERT FoodItems
    FROM 'C:\temp\fooditems.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ FoodItems imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ FoodItems import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 6: Import Bookings
PRINT 'Step 6: Importing Bookings data...'
BEGIN TRY
    BULK INSERT Bookings
    FROM 'C:\temp\bookings.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ Bookings imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ Bookings import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 7: Import BookingTickets
PRINT 'Step 7: Importing BookingTickets data...'
BEGIN TRY
    BULK INSERT BookingTickets
    FROM 'C:\temp\bookingtickets.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ BookingTickets imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ BookingTickets import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 8: Import BookingFoods
PRINT 'Step 8: Importing BookingFoods data...'
BEGIN TRY
    BULK INSERT BookingFoods
    FROM 'C:\temp\bookingfoods.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ BookingFoods imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ BookingFoods import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 9: Import Payments
PRINT 'Step 9: Importing Payments data...'
BEGIN TRY
    BULK INSERT Payments
    FROM 'C:\temp\payments.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ Payments imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ Payments import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 10: Import PaymentRecords
PRINT 'Step 10: Importing PaymentRecords data...'
BEGIN TRY
    BULK INSERT PaymentRecords
    FROM 'C:\temp\paymentrecords.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ PaymentRecords imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ PaymentRecords import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 11: Import Seats
PRINT 'Step 11: Importing Seats data...'
BEGIN TRY
    BULK INSERT Seats
    FROM 'C:\temp\seats.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ Seats imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ Seats import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 12: Import SeatReservations
PRINT 'Step 12: Importing SeatReservations data...'
BEGIN TRY
    BULK INSERT SeatReservations
    FROM 'C:\temp\seatreservations.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ SeatReservations imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ SeatReservations import failed: ' + ERROR_MESSAGE()
END CATCH

-- Step 13: Import EventBookings
PRINT 'Step 13: Importing EventBookings data...'
BEGIN TRY
    BULK INSERT EventBookings
    FROM 'C:\temp\eventbookings.dat'
    WITH (
        FIELDTERMINATOR = '|',
        ROWTERMINATOR = '\n',
        FIRSTROW = 1,
        KEEPIDENTITY
    );
    PRINT '  ✓ EventBookings imported successfully'
END TRY
BEGIN CATCH
    PRINT '  ⚠ EventBookings import failed: ' + ERROR_MESSAGE()
END CATCH

PRINT ''
PRINT '=== IMPORT COMPLETED ==='
PRINT 'Verifying imported data...'

-- Verification queries
PRINT 'Events imported:'
SELECT COUNT(*) as EventCount FROM Events WHERE Id IN (4,6,19,21);

PRINT 'Event details:'
SELECT Id, Title, OrganizerId FROM Events WHERE Id IN (4,6,19,21);

PRINT 'TicketTypes imported:'
SELECT COUNT(*) as TicketTypeCount FROM TicketTypes WHERE EventId IN (4,6,19,21);

PRINT 'Bookings imported:'
SELECT COUNT(*) as BookingCount FROM Bookings WHERE EventId IN (4,6,19,21);

PRINT 'Payments imported:'
SELECT COUNT(*) as PaymentCount FROM Payments WHERE EventId IN (4,6,19,21);

PRINT ''
PRINT 'Data migration completed successfully!'
