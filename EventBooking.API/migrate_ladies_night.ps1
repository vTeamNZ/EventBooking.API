# PowerShell script to export Event ID 4 "Ladies Night" from kwdb01 and import to kwdb02
# Run this script from the EventBooking.API directory

param(
    [string]$Server = "kwserver02.database.windows.net",
    [string]$SourceDatabase = "kwdb01",
    [string]$TargetDatabase = "kwdb02",
    [string]$Username = "",
    [string]$Password = ""
)

Write-Host "=====================================================" -ForegroundColor Green
Write-Host "EVENT DATA MIGRATION: kwdb01 -> kwdb02" -ForegroundColor Green
Write-Host "Event ID 4: Ladies Night" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green

# Check if sqlcmd is available
try {
    $sqlcmdVersion = sqlcmd -?
    Write-Host "✓ SQL Server Command Line Utility found" -ForegroundColor Green
} catch {
    Write-Host "✗ SQL Server Command Line Utility (sqlcmd) not found" -ForegroundColor Red
    Write-Host "Please install SQL Server Command Line Utilities" -ForegroundColor Yellow
    exit 1
}

# Get credentials if not provided
if ([string]::IsNullOrEmpty($Username)) {
    $Username = Read-Host "Enter Azure SQL Username"
}

if ([string]::IsNullOrEmpty($Password)) {
    $SecurePassword = Read-Host "Enter Azure SQL Password" -AsSecureString
    $Password = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecurePassword))
}

$ConnectionStringBase = "Server=$Server;Database={0};User Id=$Username;Password=$Password;Encrypt=True;TrustServerCertificate=False;"
$SourceConnectionString = $ConnectionStringBase -f $SourceDatabase
$TargetConnectionString = $ConnectionStringBase -f $TargetDatabase

Write-Host ""
Write-Host "STEP 1: Generating export script from kwdb01..." -ForegroundColor Yellow

# Run the export script on kwdb01
$ExportOutputFile = "ladies_night_export_data.sql"

try {
    sqlcmd -S $Server -d $SourceDatabase -U $Username -P $Password -E -i "export_ladies_night_kwdb01.sql" -o $ExportOutputFile
    Write-Host "✓ Export script generated successfully: $ExportOutputFile" -ForegroundColor Green
} catch {
    Write-Host "✗ Failed to generate export script" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "STEP 2: Checking if data already exists in kwdb02..." -ForegroundColor Yellow

# Create a check script
$CheckScript = @"
USE $TargetDatabase;
SELECT 'Event exists' as Status, COUNT(*) as Count FROM Events WHERE Id = 4;
SELECT 'TicketTypes exist' as Status, COUNT(*) as Count FROM TicketTypes WHERE EventId = 4;
SELECT 'Seats exist' as Status, COUNT(*) as Count FROM Seats WHERE EventId = 4;
"@

$CheckOutputFile = "check_existing_data.txt"
$CheckScript | Out-File -FilePath "check_existing_data.sql" -Encoding UTF8

try {
    sqlcmd -S $Server -d $TargetDatabase -U $Username -P $Password -i "check_existing_data.sql" -o $CheckOutputFile
    $CheckResults = Get-Content $CheckOutputFile -Raw
    Write-Host "Current data in kwdb02:" -ForegroundColor Cyan
    Write-Host $CheckResults -ForegroundColor White
} catch {
    Write-Host "⚠ Could not check existing data, proceeding with import..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "STEP 3: Creating import script for kwdb02..." -ForegroundColor Yellow

# Process the export file to create proper import statements
$ImportScript = @"
USE $TargetDatabase;

-- Disable foreign key checks temporarily
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

-- Delete existing data for Event ID 4 if it exists
DELETE FROM SeatReservations WHERE EventId = 4;
DELETE FROM Reservations WHERE EventId = 4;
DELETE FROM BookingFoods WHERE BookingId IN (SELECT Id FROM Bookings WHERE EventId = 4);
DELETE FROM BookingTickets WHERE BookingId IN (SELECT Id FROM Bookings WHERE EventId = 4);
DELETE FROM Bookings WHERE EventId = 4;
DELETE FROM Seats WHERE EventId = 4;
DELETE FROM Tables WHERE EventId = 4;
DELETE FROM FoodItems WHERE EventId = 4;
DELETE FROM TicketTypes WHERE EventId = 4;
DELETE FROM Events WHERE Id = 4;

PRINT 'Existing Event ID 4 data cleaned up';

"@

# Read the exported data and clean it up
if (Test-Path $ExportOutputFile) {
    $ExportedData = Get-Content $ExportOutputFile -Raw
    
    # Extract only the INSERT statements
    $InsertStatements = $ExportedData -split "`n" | Where-Object { $_ -like "INSERT INTO*" }
    
    if ($InsertStatements.Count -gt 0) {
        $ImportScript += "`n-- IMPORTED DATA:`n"
        $ImportScript += ($InsertStatements -join "`n")
        $ImportScript += "`n"
    } else {
        Write-Host "⚠ No INSERT statements found in export file" -ForegroundColor Yellow
    }
} else {
    Write-Host "✗ Export file not found: $ExportOutputFile" -ForegroundColor Red
    exit 1
}

$ImportScript += @"

-- Re-enable foreign key checks
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

PRINT 'Event ID 4 "Ladies Night" import completed successfully';

-- Verify import
SELECT 'Events imported' as Status, COUNT(*) as Count FROM Events WHERE Id = 4;
SELECT 'TicketTypes imported' as Status, COUNT(*) as Count FROM TicketTypes WHERE EventId = 4;
SELECT 'FoodItems imported' as Status, COUNT(*) as Count FROM FoodItems WHERE EventId = 4;
SELECT 'Seats imported' as Status, COUNT(*) as Count FROM Seats WHERE EventId = 4;
SELECT 'Tables imported' as Status, COUNT(*) as Count FROM Tables WHERE EventId = 4;
"@

$ImportScriptFile = "import_ladies_night_kwdb02.sql"
$ImportScript | Out-File -FilePath $ImportScriptFile -Encoding UTF8

Write-Host "✓ Import script created: $ImportScriptFile" -ForegroundColor Green

Write-Host ""
$Confirmation = Read-Host "STEP 4: Ready to import data to kwdb02. Continue? (y/N)"

if ($Confirmation -eq 'y' -or $Confirmation -eq 'Y') {
    Write-Host "Importing data to kwdb02..." -ForegroundColor Yellow
    
    $ImportOutputFile = "import_results.txt"
    
    try {
        sqlcmd -S $Server -d $TargetDatabase -U $Username -P $Password -i $ImportScriptFile -o $ImportOutputFile
        
        Write-Host "✓ Import completed!" -ForegroundColor Green
        
        # Show import results
        if (Test-Path $ImportOutputFile) {
            $ImportResults = Get-Content $ImportOutputFile -Raw
            Write-Host ""
            Write-Host "IMPORT RESULTS:" -ForegroundColor Green
            Write-Host $ImportResults -ForegroundColor White
        }
        
        Write-Host ""
        Write-Host "=====================================================" -ForegroundColor Green
        Write-Host "MIGRATION COMPLETED SUCCESSFULLY!" -ForegroundColor Green
        Write-Host "Event ID 4 'Ladies Night' has been migrated from kwdb01 to kwdb02" -ForegroundColor Green
        Write-Host "=====================================================" -ForegroundColor Green
        
    } catch {
        Write-Host "✗ Import failed" -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "Import cancelled by user" -ForegroundColor Yellow
    Write-Host "Files created for manual review:" -ForegroundColor Cyan
    Write-Host "- $ExportOutputFile (exported data)" -ForegroundColor White
    Write-Host "- $ImportScriptFile (import script)" -ForegroundColor White
}

# Cleanup temporary files
Write-Host ""
Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow
Remove-Item -Path "check_existing_data.sql" -ErrorAction SilentlyContinue
Remove-Item -Path $CheckOutputFile -ErrorAction SilentlyContinue

Write-Host "Migration script completed!" -ForegroundColor Green
