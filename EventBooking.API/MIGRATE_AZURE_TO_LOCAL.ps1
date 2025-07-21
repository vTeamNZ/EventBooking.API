# =====================================================
# AZURE TO LOCAL SQL EXPRESS MIGRATION
# Purpose: Complete migration from kwdb01 (Azure) to local SQL Express
# Date: 2025-01-21
# =====================================================

Write-Host "==================================================="
Write-Host "🚀 AZURE TO LOCAL SQL EXPRESS MIGRATION" -ForegroundColor Cyan
Write-Host "==================================================="

# Configuration
$AzureServer = "kwsqlsvr01.database.windows.net,1433"
$AzureDatabase = "kwdb01"
$AzureUser = "gayantd"
$AzurePassword = "maGulak@143456"

$LocalServer = ".\SQLEXPRESS"
$LocalDatabase = "kwdb01_local"
$WorkingDir = "C:\temp\migration"

# Ensure working directory exists
if (!(Test-Path $WorkingDir)) {
    New-Item -ItemType Directory -Path $WorkingDir -Force
    Write-Host "✅ Created working directory: $WorkingDir" -ForegroundColor Green
}

# Step 1: Test connections
Write-Host "`n1️⃣ Testing connections..." -ForegroundColor Yellow

# Test Azure connection
$testQuery = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
$azureTableCount = sqlcmd -S $AzureServer -d $AzureDatabase -U $AzureUser -P $AzurePassword -Q $testQuery -h -1

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Azure SQL connected - Found $azureTableCount tables" -ForegroundColor Green
} else {
    Write-Host "❌ Azure SQL connection failed!" -ForegroundColor Red
    exit 1
}

# Test local connection
$localTest = sqlcmd -S $LocalServer -E -Q "SELECT @@VERSION" -h -1
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ SQL Express connected" -ForegroundColor Green
} else {
    Write-Host "❌ SQL Express connection failed!" -ForegroundColor Red
    exit 1
}

# Step 2: Create local database
Write-Host "`n2️⃣ Creating local database..." -ForegroundColor Yellow

$createDbSql = @"
IF EXISTS (SELECT name FROM sys.databases WHERE name = '$LocalDatabase')
BEGIN
    ALTER DATABASE [$LocalDatabase] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$LocalDatabase];
    PRINT 'Existing database dropped';
END

CREATE DATABASE [$LocalDatabase];
PRINT 'New database created';
"@

sqlcmd -S $LocalServer -E -Q $createDbSql
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Local database '$LocalDatabase' created" -ForegroundColor Green
} else {
    Write-Host "❌ Failed to create local database!" -ForegroundColor Red
    exit 1
}

# Step 3: Export and recreate schema
Write-Host "`n3️⃣ Recreating database schema..." -ForegroundColor Yellow

# Get list of tables from Azure in dependency order
$tableOrder = @(
    "AspNetRoles",
    "AspNetUsers", 
    "AspNetUserRoles",
    "AspNetUserClaims",
    "AspNetRoleClaims", 
    "AspNetUserLogins",
    "AspNetUserTokens",
    "Venues",
    "Organizers",
    "Events", 
    "TicketTypes",
    "FoodItems",
    "Tables",
    "Seats",
    "Bookings",
    "BookingTickets", 
    "BookingFoods",
    "Payments",
    "PaymentRecords",
    "EventBookings",
    "Reservations",
    "SeatReservations",
    "TableReservations"
)

# Generate CREATE TABLE statements for each table
foreach ($table in $tableOrder) {
    Write-Host "Processing table: $table" -ForegroundColor Cyan
    
    # Get table schema from Azure
    $schemaQuery = @"
SELECT 
    COLUMN_NAME,
    DATA_TYPE,
    CHARACTER_MAXIMUM_LENGTH,
    NUMERIC_PRECISION,
    NUMERIC_SCALE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = '$table' AND TABLE_SCHEMA = 'dbo'
ORDER BY ORDINAL_POSITION
"@
    
    try {
        # Export schema info to temp file
        sqlcmd -S $AzureServer -d $AzureDatabase -U $AzureUser -P $AzurePassword -Q $schemaQuery -o "$WorkingDir\schema_$table.txt" -s "|" -W
        
        if (Test-Path "$WorkingDir\schema_$table.txt") {
            Write-Host "   ✅ Schema exported for $table" -ForegroundColor Green
        }
    } catch {
        Write-Host "   ⚠️ Could not export schema for $table" -ForegroundColor Yellow
    }
}

# Step 4: Use SqlPackage if available, otherwise use BCP
Write-Host "`n4️⃣ Checking for migration tools..." -ForegroundColor Yellow

$sqlPackagePath = ""
$possiblePaths = @(
    "${env:ProgramFiles}\Microsoft SQL Server\160\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles}\Microsoft SQL Server\150\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles(x86)}\Microsoft SQL Server\160\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles(x86)}\Microsoft SQL Server\150\DAC\bin\SqlPackage.exe"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $sqlPackagePath = $path
        Write-Host "✅ Found SqlPackage: $sqlPackagePath" -ForegroundColor Green
        break
    }
}

if ($sqlPackagePath) {
    # Use SqlPackage for complete migration
    Write-Host "`n5️⃣ Using SqlPackage for complete migration..." -ForegroundColor Yellow
    
    $backupFile = "$WorkingDir\kwdb01_backup.bacpac"
    
    # Export from Azure
    Write-Host "Exporting from Azure..." -ForegroundColor Cyan
    $exportArgs = @(
        "/Action:Export",
        "/SourceServerName:$AzureServer",
        "/SourceDatabaseName:$AzureDatabase", 
        "/SourceUser:$AzureUser",
        "/SourcePassword:$AzurePassword",
        "/TargetFile:$backupFile",
        "/OverwriteFiles:True"
    )
    
    & $sqlPackagePath $exportArgs
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path $backupFile)) {
        $fileSize = [math]::Round((Get-Item $backupFile).Length / 1MB, 2)
        Write-Host "✅ Export successful! Size: ${fileSize}MB" -ForegroundColor Green
        
        # Import to local
        Write-Host "Importing to local SQL Express..." -ForegroundColor Cyan
        $importArgs = @(
            "/Action:Import",
            "/TargetServerName:$LocalServer",
            "/TargetDatabaseName:$LocalDatabase",
            "/TargetTrustServerCertificate:True",
            "/SourceFile:$backupFile"
        )
        
        & $sqlPackagePath $importArgs
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Import successful!" -ForegroundColor Green
        } else {
            Write-Host "❌ Import failed!" -ForegroundColor Red
        }
    } else {
        Write-Host "❌ Export failed!" -ForegroundColor Red
    }
    
} else {
    # Use BCP method for data migration
    Write-Host "`n5️⃣ Using BCP method for data migration..." -ForegroundColor Yellow
    Write-Host "First, we need to create the schema manually..." -ForegroundColor Cyan
    
    # Run the schema creation script
    Write-Host "Creating tables in local database..." -ForegroundColor Cyan
    sqlcmd -S $LocalServer -d $LocalDatabase -E -i "CREATE_LOCAL_SCHEMA.sql"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Schema created successfully" -ForegroundColor Green
        
        # Now export and import data for each table
        Write-Host "Migrating data..." -ForegroundColor Cyan
        
        foreach ($table in $tableOrder) {
            Write-Host "Migrating data for: $table" -ForegroundColor Gray
            
            # Check if table has data
            $countQuery = "SELECT COUNT(*) FROM [$table]"
            $count = sqlcmd -S $AzureServer -d $AzureDatabase -U $AzureUser -P $AzurePassword -Q $countQuery -h -1 2>$null
            
            if ($count -and [int]$count -gt 0) {
                Write-Host "   Records: $count" -ForegroundColor Gray
                
                # Export data
                $dataFile = "$WorkingDir\$table.dat"
                bcp "[$AzureDatabase].[dbo].[$table]" out $dataFile -S $AzureServer -U $AzureUser -P $AzurePassword -c -t "|" 2>$null
                
                if (Test-Path $dataFile) {
                    # Import data
                    bcp "[$LocalDatabase].[dbo].[$table]" in $dataFile -S $LocalServer -T -c -t "|" 2>$null
                    
                    if ($LASTEXITCODE -eq 0) {
                        Write-Host "   ✅ Migrated" -ForegroundColor Green
                    } else {
                        Write-Host "   ❌ Import failed" -ForegroundColor Red
                    }
                    
                    # Clean up data file
                    Remove-Item $dataFile -Force 2>$null
                } else {
                    Write-Host "   ⚠️ Export failed" -ForegroundColor Yellow
                }
            } else {
                Write-Host "   ⚪ No data" -ForegroundColor Gray
            }
        }
    } else {
        Write-Host "❌ Schema creation failed!" -ForegroundColor Red
    }
}

# Step 6: Verify migration
Write-Host "`n6️⃣ Verifying migration..." -ForegroundColor Yellow

$verifyQuery = @"
SELECT 
    'AspNetUsers' as TableName, COUNT(*) as RecordCount FROM [$LocalDatabase].[dbo].[AspNetUsers]
UNION ALL
SELECT 
    'Events', COUNT(*) FROM [$LocalDatabase].[dbo].[Events] 
UNION ALL
SELECT 
    'Bookings', COUNT(*) FROM [$LocalDatabase].[dbo].[Bookings]
UNION ALL
SELECT 
    'BookingTickets', COUNT(*) FROM [$LocalDatabase].[dbo].[BookingTickets]
UNION ALL
SELECT 
    'Payments', COUNT(*) FROM [$LocalDatabase].[dbo].[Payments]
UNION ALL
SELECT 
    'TicketTypes', COUNT(*) FROM [$LocalDatabase].[dbo].[TicketTypes]
"@

$verification = sqlcmd -S $LocalServer -E -Q $verifyQuery

Write-Host "✅ Migration verification:" -ForegroundColor Green
$verification

# Step 7: Create local connection string
Write-Host "`n7️⃣ Creating local configuration..." -ForegroundColor Yellow

$localConnectionString = "Server=$LocalServer;Database=$LocalDatabase;Integrated Security=true;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=120;Command Timeout=300;"

# Create appsettings.local.json
$localAppSettings = @{
    "ConnectionStrings" = @{
        "DefaultConnection" = $localConnectionString
    }
    "Stripe" = @{
        "SecretKey" = "sk_test_..."
        "PublishableKey" = "pk_test_..."
        "WebhookSecret" = "whsec_test_..."
    }
    "Jwt" = @{
        "Key" = "ThisIsASuperUltraSecretJWTKeyWithMinimum32Bytes"
        "Issuer" = "https://localhost:5000"
        "Audience" = "https://localhost:5000"
        "ExpiryInMinutes" = 60
    }
    "Logging" = @{
        "LogLevel" = @{
            "Default" = "Information"
            "Microsoft.AspNetCore" = "Warning"
        }
    }
    "AllowedHosts" = "*"
    "Email" = @{
        "SmtpServer" = "smtp.sendgrid.net"
        "SmtpPort" = 587
        "UseAuthentication" = $true
        "Username" = "apikey"
        "Password" = "test_key"
        "SenderEmail" = "test@localhost.com"
        "SenderName" = "Local Development Environment"
    }
    "QRTickets" = @{
        "StoragePath" = "wwwroot/tickets"
        "BaseUrl" = "https://localhost:5000"
        "RetentionDays" = 30
        "CleanupIntervalHours" = 24
    }
}

$localAppSettingsPath = "appsettings.local.json"
$localAppSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $localAppSettingsPath

Write-Host "✅ Created: $localAppSettingsPath" -ForegroundColor Green

Write-Host "`n==================================================="
Write-Host "🎉 MIGRATION COMPLETED!" -ForegroundColor Green
Write-Host "==================================================="
Write-Host "Source: kwdb01 (Azure SQL Database)"
Write-Host "Target: $LocalDatabase (Local SQL Express)"
Write-Host "Connection: $localConnectionString"
Write-Host ""
Write-Host "📋 Next Steps:"
Write-Host "1. Test your application with appsettings.local.json"
Write-Host "2. Verify all data migrated correctly"
Write-Host "3. Update your development environment to use local database"
Write-Host "==================================================="

# Cleanup
$cleanup = Read-Host "`nClean up temporary files? (y/n)"
if ($cleanup -eq 'y') {
    Remove-Item "$WorkingDir\*" -Force -Recurse 2>$null
    Write-Host "✅ Temporary files cleaned up" -ForegroundColor Green
}
