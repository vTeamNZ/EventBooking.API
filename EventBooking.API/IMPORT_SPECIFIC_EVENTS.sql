-- Import specific events and all related data into kwdb01_local
-- Events: 4, 6, 19, 21

PRINT '=== IMPORTING EVENTS 4, 6, 19, 21 AND ALL RELATED DATA ==='
PRINT ''

-- Step 1: Import Organizers first (needed for Events foreign key)
PRINT 'Step 1: Importing Organizers data...'
BULK INSERT kwdb01_local.dbo.Organizers
FROM 'C:\temp\organizers_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 2: Import Events
PRINT 'Step 2: Importing Events data...'
BULK INSERT kwdb01_local.dbo.Events
FROM 'C:\temp\events_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 3: Import TicketTypes
PRINT 'Step 3: Importing TicketTypes data...'
BULK INSERT kwdb01_local.dbo.TicketTypes
FROM 'C:\temp\tickettypes_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 4: Import FoodItems
PRINT 'Step 4: Importing FoodItems data...'
BULK INSERT kwdb01_local.dbo.FoodItems
FROM 'C:\temp\fooditems_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 5: Import Bookings
PRINT 'Step 5: Importing Bookings data...'
BULK INSERT kwdb01_local.dbo.Bookings
FROM 'C:\temp\bookings_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 6: Import BookingTickets
PRINT 'Step 6: Importing BookingTickets data...'
BULK INSERT kwdb01_local.dbo.BookingTickets
FROM 'C:\temp\bookingtickets_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 7: Import BookingFoods
PRINT 'Step 7: Importing BookingFoods data...'
BULK INSERT kwdb01_local.dbo.BookingFoods
FROM 'C:\temp\bookingfoods_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 8: Import Payments
PRINT 'Step 8: Importing Payments data...'
BULK INSERT kwdb01_local.dbo.Payments
FROM 'C:\temp\payments_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 9: Import PaymentRecords
PRINT 'Step 9: Importing PaymentRecords data...'
BULK INSERT kwdb01_local.dbo.PaymentRecords
FROM 'C:\temp\paymentrecords_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 10: Import Seats
PRINT 'Step 10: Importing Seats data...'
BULK INSERT kwdb01_local.dbo.Seats
FROM 'C:\temp\seats_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 11: Import SeatReservations
PRINT 'Step 11: Importing SeatReservations data...'
BULK INSERT kwdb01_local.dbo.SeatReservations
FROM 'C:\temp\seatreservations_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

-- Step 12: Import EventBookings
PRINT 'Step 12: Importing EventBookings data...'
BULK INSERT kwdb01_local.dbo.EventBookings
FROM 'C:\temp\eventbookings_export.dat'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1
);

PRINT ''
PRINT '=== IMPORT COMPLETED ==='
PRINT 'Verifying imported data...'

-- Verification queries
PRINT 'Events imported:'
SELECT COUNT(*) as EventCount FROM Events WHERE Id IN (4,6,19,21);

PRINT 'TicketTypes imported:'
SELECT COUNT(*) as TicketTypeCount FROM TicketTypes WHERE EventId IN (4,6,19,21);

PRINT 'Bookings imported:'
SELECT COUNT(*) as BookingCount FROM Bookings WHERE EventId IN (4,6,19,21);

PRINT 'Payments imported:'
SELECT COUNT(*) as PaymentCount FROM Payments WHERE EventId IN (4,6,19,21);

PRINT ''
PRINT 'Data migration completed successfully!'
