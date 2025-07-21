# Complete Migration Script for Events 4, 6, 19, 21
# Migrates all related data from Azure kwdb01 to local kwdb01_local

Write-Host "=== STARTING MIGRATION OF EVENTS 4, 6, 19, 21 ===" -ForegroundColor Green
Write-Host ""

# Create temp directory if it doesn't exist
$tempDir = "C:\temp"
if (!(Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force
    Write-Host "Created temp directory: $tempDir" -ForegroundColor Yellow
}

# Azure SQL connection details
$azureServer = "kwsqlsvr01.database.windows.net,1433"
$azureUser = "gayantd"
$azurePassword = "maGulak@143456"
$azureDb = "kwdb01"

# Local SQL Express details
$localServer = ".\SQLEXPRESS"
$localDb = "kwdb01_local"

Write-Host "Step 1: Exporting data from Azure SQL..." -ForegroundColor Cyan

# Function to export data using BCP
function Export-TableData {
    param($query, $filename, $description)
    Write-Host "  Exporting $description..." -ForegroundColor Yellow
    $fullPath = "$tempDir\$filename"
    $bcpCmd = "bcp `"$query`" queryout `"$fullPath`" -S `"$azureServer`" -U `"$azureUser`" -P `"$azurePassword`" -c -t `"|`""
    Invoke-Expression $bcpCmd
    if ($LASTEXITCODE -eq 0) {
        Write-Host "    ✓ $description exported successfully" -ForegroundColor Green
    } else {
        Write-Host "    ✗ Failed to export $description" -ForegroundColor Red
    }
}

# Export all related data
Export-TableData "SELECT * FROM $azureDb.dbo.Events WHERE Id IN (4,6,19,21)" "events.dat" "Events"
Export-TableData "SELECT DISTINCT u.* FROM $azureDb.dbo.AspNetUsers u INNER JOIN $azureDb.dbo.Organizers o ON u.Id = o.UserId INNER JOIN $azureDb.dbo.Events e ON o.Id = e.OrganizerId WHERE e.Id IN (4,6,19,21)" "aspnetusers.dat" "AspNetUsers"
Export-TableData "SELECT DISTINCT o.* FROM $azureDb.dbo.Organizers o INNER JOIN $azureDb.dbo.Events e ON o.Id = e.OrganizerId WHERE e.Id IN (4,6,19,21)" "organizers.dat" "Organizers"
Export-TableData "SELECT * FROM $azureDb.dbo.TicketTypes WHERE EventId IN (4,6,19,21)" "tickettypes.dat" "TicketTypes"
Export-TableData "SELECT * FROM $azureDb.dbo.FoodItems WHERE EventId IN (4,6,19,21)" "fooditems.dat" "FoodItems"
Export-TableData "SELECT * FROM $azureDb.dbo.Bookings WHERE EventId IN (4,6,19,21)" "bookings.dat" "Bookings"
Export-TableData "SELECT bt.* FROM $azureDb.dbo.BookingTickets bt INNER JOIN $azureDb.dbo.Bookings b ON bt.BookingId = b.Id WHERE b.EventId IN (4,6,19,21)" "bookingtickets.dat" "BookingTickets"
Export-TableData "SELECT bf.* FROM $azureDb.dbo.BookingFoods bf INNER JOIN $azureDb.dbo.Bookings b ON bf.BookingId = b.Id WHERE b.EventId IN (4,6,19,21)" "bookingfoods.dat" "BookingFoods"
Export-TableData "SELECT * FROM $azureDb.dbo.Payments WHERE EventId IN (4,6,19,21)" "payments.dat" "Payments"
Export-TableData "SELECT * FROM $azureDb.dbo.PaymentRecords WHERE EventId IN (4,6,19,21)" "paymentrecords.dat" "PaymentRecords"
Export-TableData "SELECT * FROM $azureDb.dbo.Seats WHERE EventId IN (4,6,19,21)" "seats.dat" "Seats"
Export-TableData "SELECT * FROM $azureDb.dbo.SeatReservations WHERE EventId IN (4,6,19,21)" "seatreservations.dat" "SeatReservations"
Export-TableData "SELECT * FROM $azureDb.dbo.EventBookings WHERE EventID IN ('4','6','19','21')" "eventbookings.dat" "EventBookings"

Write-Host ""
Write-Host "Step 2: Importing data into local SQL Express..." -ForegroundColor Cyan

# Function to import data using BULK INSERT
function Import-TableData {
    param($tableName, $filename, $description)
    Write-Host "  Importing $description..." -ForegroundColor Yellow
    $fullPath = "$tempDir\$filename"
    
    if (Test-Path $fullPath) {
        $query = @"
BULK INSERT [$localDb].[dbo].[$tableName]
FROM '$fullPath'
WITH (
    FIELDTERMINATOR = '|',
    ROWTERMINATOR = '\n',
    FIRSTROW = 1,
    KEEPIDENTITY
);
"@
        sqlcmd -S $localServer -E -Q $query
        if ($LASTEXITCODE -eq 0) {
            Write-Host "    ✓ $description imported successfully" -ForegroundColor Green
        } else {
            Write-Host "    ✗ Failed to import $description" -ForegroundColor Red
        }
    } else {
        Write-Host "    ⚠ File not found: $fullPath" -ForegroundColor Yellow
    }
}

# Import in correct order (respecting foreign key dependencies)
Import-TableData "AspNetUsers" "aspnetusers.dat" "AspNetUsers"
Import-TableData "Organizers" "organizers.dat" "Organizers"
Import-TableData "Events" "events.dat" "Events"
Import-TableData "TicketTypes" "tickettypes.dat" "TicketTypes"
Import-TableData "FoodItems" "fooditems.dat" "FoodItems"
Import-TableData "Bookings" "bookings.dat" "Bookings"
Import-TableData "BookingTickets" "bookingtickets.dat" "BookingTickets"
Import-TableData "BookingFoods" "bookingfoods.dat" "BookingFoods"
Import-TableData "Payments" "payments.dat" "Payments"
Import-TableData "PaymentRecords" "paymentrecords.dat" "PaymentRecords"
Import-TableData "Seats" "seats.dat" "Seats"
Import-TableData "SeatReservations" "seatreservations.dat" "SeatReservations"
Import-TableData "EventBookings" "eventbookings.dat" "EventBookings"

Write-Host ""
Write-Host "Step 3: Verifying imported data..." -ForegroundColor Cyan

# Verification queries
$verificationQueries = @(
    "SELECT COUNT(*) as EventCount FROM Events WHERE Id IN (4,6,19,21)",
    "SELECT COUNT(*) as TicketTypeCount FROM TicketTypes WHERE EventId IN (4,6,19,21)",
    "SELECT COUNT(*) as BookingCount FROM Bookings WHERE EventId IN (4,6,19,21)",
    "SELECT COUNT(*) as PaymentCount FROM Payments WHERE EventId IN (4,6,19,21)",
    "SELECT COUNT(*) as SeatCount FROM Seats WHERE EventId IN (4,6,19,21)",
    "SELECT Id, Title FROM Events WHERE Id IN (4,6,19,21)"
)

foreach ($query in $verificationQueries) {
    Write-Host "  Running: $query" -ForegroundColor Yellow
    sqlcmd -S $localServer -E -d $localDb -Q $query
}

Write-Host ""
Write-Host "=== MIGRATION COMPLETED ===" -ForegroundColor Green
Write-Host "Events 4, 6, 19, 21 and all related data have been migrated to kwdb01_local" -ForegroundColor Green
Write-Host ""
Write-Host "You can now test your application with these specific events!" -ForegroundColor Cyan
