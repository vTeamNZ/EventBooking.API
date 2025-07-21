@echo off
echo === MIGRATING EVENTS 4, 6, 19, 21 AND ALL RELATED DATA ===
echo.

echo Step 1: Creating temp directory...
if not exist "C:\temp" mkdir "C:\temp"

echo Step 2: Exporting data from Azure SQL...

echo   Exporting AspNetUsers...
bcp "SELECT DISTINCT u.* FROM AspNetUsers u INNER JOIN Organizers o ON u.Id = o.UserId INNER JOIN Events e ON o.Id = e.OrganizerId WHERE e.Id IN (4,6,19,21)" queryout "C:\temp\aspnetusers.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting Events...
bcp "SELECT * FROM Events WHERE Id IN (4,6,19,21)" queryout "C:\temp\events.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting Organizers...
bcp "SELECT DISTINCT o.* FROM Organizers o INNER JOIN Events e ON o.Id = e.OrganizerId WHERE e.Id IN (4,6,19,21)" queryout "C:\temp\organizers.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting TicketTypes...
bcp "SELECT * FROM TicketTypes WHERE EventId IN (4,6,19,21)" queryout "C:\temp\tickettypes.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting FoodItems...
bcp "SELECT * FROM FoodItems WHERE EventId IN (4,6,19,21)" queryout "C:\temp\fooditems.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting Bookings...
bcp "SELECT * FROM Bookings WHERE EventId IN (4,6,19,21)" queryout "C:\temp\bookings.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting BookingTickets...
bcp "SELECT bt.* FROM BookingTickets bt INNER JOIN Bookings b ON bt.BookingId = b.Id WHERE b.EventId IN (4,6,19,21)" queryout "C:\temp\bookingtickets.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting BookingFoods...
bcp "SELECT bf.* FROM BookingFoods bf INNER JOIN Bookings b ON bf.BookingId = b.Id WHERE b.EventId IN (4,6,19,21)" queryout "C:\temp\bookingfoods.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting Payments...
bcp "SELECT * FROM Payments WHERE EventId IN (4,6,19,21)" queryout "C:\temp\payments.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting PaymentRecords...
bcp "SELECT * FROM PaymentRecords WHERE EventId IN (4,6,19,21)" queryout "C:\temp\paymentrecords.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting Seats...
bcp "SELECT * FROM Seats WHERE EventId IN (4,6,19,21)" queryout "C:\temp\seats.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting SeatReservations...
bcp "SELECT * FROM SeatReservations WHERE EventId IN (4,6,19,21)" queryout "C:\temp\seatreservations.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo   Exporting EventBookings...
bcp "SELECT * FROM EventBookings WHERE EventID IN ('4','6','19','21')" queryout "C:\temp\eventbookings.dat" -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -c -t "|"

echo.
echo Export completed! Running import script...
echo.

sqlcmd -S ".\SQLEXPRESS" -E -d "kwdb01_local" -i "IMPORT_SPECIFIC_EVENTS_SIMPLE.sql"

echo.
echo === MIGRATION COMPLETED ===
echo Events 4, 6, 19, 21 and all related data have been migrated!
pause
