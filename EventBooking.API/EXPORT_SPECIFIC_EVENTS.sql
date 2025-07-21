-- Migrate specific events and all related data from kwdb01 (Azure) to kwdb01_local
-- Events: 4, 6, 19, 21

PRINT '=== MIGRATING EVENTS 4, 6, 19, 21 AND ALL RELATED DATA ==='
PRINT ''

-- Step 1: Export Events data
PRINT 'Step 1: Exporting Events data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.Events WHERE Id IN (4,6,19,21)" queryout "C:\temp\events_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 2: Export Organizers (needed for Events foreign key)
PRINT 'Step 2: Exporting Organizers data...'
EXEC('bcp "SELECT DISTINCT o.* FROM kwdb01.dbo.Organizers o INNER JOIN kwdb01.dbo.Events e ON o.Id = e.OrganizerId WHERE e.Id IN (4,6,19,21)" queryout "C:\temp\organizers_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 3: Export TicketTypes for these events
PRINT 'Step 3: Exporting TicketTypes data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.TicketTypes WHERE EventId IN (4,6,19,21)" queryout "C:\temp\tickettypes_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 4: Export FoodItems for these events
PRINT 'Step 4: Exporting FoodItems data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.FoodItems WHERE EventId IN (4,6,19,21)" queryout "C:\temp\fooditems_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 5: Export Bookings for these events
PRINT 'Step 5: Exporting Bookings data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.Bookings WHERE EventId IN (4,6,19,21)" queryout "C:\temp\bookings_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 6: Export BookingTickets (linked to Bookings)
PRINT 'Step 6: Exporting BookingTickets data...'
EXEC('bcp "SELECT bt.* FROM kwdb01.dbo.BookingTickets bt INNER JOIN kwdb01.dbo.Bookings b ON bt.BookingId = b.Id WHERE b.EventId IN (4,6,19,21)" queryout "C:\temp\bookingtickets_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 7: Export BookingFoods (linked to Bookings)
PRINT 'Step 7: Exporting BookingFoods data...'
EXEC('bcp "SELECT bf.* FROM kwdb01.dbo.BookingFoods bf INNER JOIN kwdb01.dbo.Bookings b ON bf.BookingId = b.Id WHERE b.EventId IN (4,6,19,21)" queryout "C:\temp\bookingfoods_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 8: Export Payments for these events
PRINT 'Step 8: Exporting Payments data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.Payments WHERE EventId IN (4,6,19,21)" queryout "C:\temp\payments_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 9: Export PaymentRecords for these events
PRINT 'Step 9: Exporting PaymentRecords data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.PaymentRecords WHERE EventId IN (4,6,19,21)" queryout "C:\temp\paymentrecords_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 10: Export Seats for these events
PRINT 'Step 10: Exporting Seats data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.Seats WHERE EventId IN (4,6,19,21)" queryout "C:\temp\seats_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 11: Export SeatReservations for these events
PRINT 'Step 11: Exporting SeatReservations data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.SeatReservations WHERE EventId IN (4,6,19,21)" queryout "C:\temp\seatreservations_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

-- Step 12: Export EventBookings for these events
PRINT 'Step 12: Exporting EventBookings data...'
EXEC('bcp "SELECT * FROM kwdb01.dbo.EventBookings WHERE EventID IN (''4'',''6'',''19'',''21'')" queryout "C:\temp\eventbookings_export.dat" -S "kwsqlsvr01.database.windows.net,1433" -U "gayantd" -P "maGulak@143456" -c -t "|"')

PRINT ''
PRINT '=== EXPORT COMPLETED ==='
PRINT 'All data files have been exported to C:\temp\'
PRINT 'Next step: Run the IMPORT script to load data into local database'
