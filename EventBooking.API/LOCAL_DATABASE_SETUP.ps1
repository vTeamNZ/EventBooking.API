# =====================================================
# LOCAL DATABASE SETUP SCRIPT
# Purpose: Export kwdb01 from Azure and import to local SQL Express
# Date: 2025-01-21
# =====================================================

Write-Host "==================================================="
Write-Host "LOCAL DATABASE SETUP FOR MIGRATION TESTING"
Write-Host "==================================================="

# Configuration
$AzureServer = "kwsqlsvr01.database.windows.net,1433"
$AzureDatabase = "kwdb01"
$AzureUser = "gayantd"
$AzurePassword = "maGulak@143456"

$LocalServer = ".\SQLEXPRESS"  # Default SQL Express instance
$LocalDatabase = "kwdb01_local"
$BackupPath = "C:\temp\kwdb01_backup.bacpac"

# Ensure temp directory exists
if (!(Test-Path "C:\temp")) {
    New-Item -ItemType Directory -Path "C:\temp" -Force
    Write-Host "Created temp directory: C:\temp"
}

Write-Host "Step 1: Testing Azure SQL connection..."
try {
    $azureConnectionString = "Server=$AzureServer;Database=$AzureDatabase;User ID=$AzureUser;Password=$AzurePassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;"
    
    # Test connection using sqlcmd
    $testQuery = "SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'"
    $result = sqlcmd -S $AzureServer -d $AzureDatabase -U $AzureUser -P $AzurePassword -Q $testQuery -h -1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Azure SQL connection successful!" -ForegroundColor Green
        Write-Host "Tables found: $result" -ForegroundColor Green
    } else {
        Write-Host "❌ Azure SQL connection failed!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error testing Azure connection: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`nStep 2: Testing local SQL Express connection..."
try {
    # Test local SQL Express connection
    $testLocalQuery = "SELECT @@VERSION"
    $localResult = sqlcmd -S $LocalServer -E -Q $testLocalQuery -h -1
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ SQL Express connection successful!" -ForegroundColor Green
        Write-Host "SQL Express Version: $($localResult -split "`n" | Select-Object -First 1)" -ForegroundColor Green
    } else {
        Write-Host "❌ SQL Express connection failed!" -ForegroundColor Red
        Write-Host "Make sure SQL Express is installed and running" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "❌ Error testing SQL Express connection: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`nStep 3: Checking if SqlPackage is available..."
$sqlPackagePath = ""

# Common SqlPackage locations
$possiblePaths = @(
    "${env:ProgramFiles}\Microsoft SQL Server\160\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles}\Microsoft SQL Server\150\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles}\Microsoft SQL Server\140\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles(x86)}\Microsoft SQL Server\160\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles(x86)}\Microsoft SQL Server\150\DAC\bin\SqlPackage.exe",
    "${env:ProgramFiles(x86)}\Microsoft SQL Server\140\DAC\bin\SqlPackage.exe"
)

foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $sqlPackagePath = $path
        Write-Host "✅ Found SqlPackage at: $sqlPackagePath" -ForegroundColor Green
        break
    }
}

if ([string]::IsNullOrEmpty($sqlPackagePath)) {
    Write-Host "❌ SqlPackage not found!" -ForegroundColor Red
    Write-Host "Please install SQL Server Data Tools (SSDT) or Azure Data Studio" -ForegroundColor Yellow
    Write-Host "Download from: https://docs.microsoft.com/en-us/sql/tools/sqlpackage" -ForegroundColor Yellow
    
    # Alternative using BCP for data export
    Write-Host "`n📋 Alternative: Using BCP for data export..." -ForegroundColor Cyan
    $useBcp = Read-Host "Would you like to use BCP method instead? (y/n)"
    
    if ($useBcp -eq 'y') {
        & "$PSScriptRoot\EXPORT_USING_BCP.ps1"
        exit
    } else {
        exit 1
    }
}

Write-Host "`nStep 4: Exporting Azure database to BACPAC..."
try {
    $exportArgs = @(
        "/Action:Export",
        "/SourceServerName:$AzureServer",
        "/SourceDatabaseName:$AzureDatabase",
        "/SourceUser:$AzureUser",
        "/SourcePassword:$AzurePassword",
        "/TargetFile:$BackupPath",
        "/OverwriteFiles:True"
    )
    
    Write-Host "Executing: $sqlPackagePath $($exportArgs -join ' ')" -ForegroundColor Cyan
    & $sqlPackagePath $exportArgs
    
    if ($LASTEXITCODE -eq 0 -and (Test-Path $BackupPath)) {
        $fileSize = [math]::Round((Get-Item $BackupPath).Length / 1MB, 2)
        Write-Host "✅ Export successful! File size: ${fileSize}MB" -ForegroundColor Green
    } else {
        Write-Host "❌ Export failed!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error during export: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`nStep 5: Creating local database..."
try {
    $createDbQuery = @"
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '$LocalDatabase')
BEGIN
    CREATE DATABASE [$LocalDatabase]
END
"@
    
    sqlcmd -S $LocalServer -E -Q $createDbQuery
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Local database '$LocalDatabase' created/verified" -ForegroundColor Green
    } else {
        Write-Host "❌ Failed to create local database!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error creating local database: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`nStep 6: Importing BACPAC to local SQL Express..."
try {
    $importArgs = @(
        "/Action:Import",
        "/TargetServerName:$LocalServer",
        "/TargetDatabaseName:$LocalDatabase",
        "/TargetTrustServerCertificate:True",
        "/SourceFile:$BackupPath",
        "/OverwriteFiles:True"
    )
    
    Write-Host "Executing: $sqlPackagePath $($importArgs -join ' ')" -ForegroundColor Cyan
    & $sqlPackagePath $importArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Import successful!" -ForegroundColor Green
    } else {
        Write-Host "❌ Import failed!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Error during import: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host "`nStep 7: Verifying imported database..."
try {
    $verifyQuery = @"
SELECT 
    'Users' as TableName, COUNT(*) as RecordCount FROM [$LocalDatabase].[dbo].[AspNetUsers]
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
"@
    
    $verification = sqlcmd -S $LocalServer -E -Q $verifyQuery -h -1
    
    Write-Host "✅ Database verification:" -ForegroundColor Green
    $verification | ForEach-Object { Write-Host "   $_" -ForegroundColor Green }
    
} catch {
    Write-Host "❌ Error verifying database: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "`nStep 8: Creating local connection string..."
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
        "SenderName" = "Local Test Environment"
    }
    "QRTickets" = @{
        "StoragePath" = "wwwroot/tickets"
        "BaseUrl" = "https://localhost:5000"
        "RetentionDays" = 30
        "CleanupIntervalHours" = 24
    }
}

$localAppSettingsPath = "$PSScriptRoot\appsettings.local.json"
$localAppSettings | ConvertTo-Json -Depth 10 | Set-Content -Path $localAppSettingsPath

Write-Host "✅ Created local app settings: $localAppSettingsPath" -ForegroundColor Green

Write-Host "`n==================================================="
Write-Host "✅ LOCAL DATABASE SETUP COMPLETED SUCCESSFULLY!"
Write-Host "==================================================="
Write-Host "Local Database: $LocalDatabase"
Write-Host "Server: $LocalServer"
Write-Host "Connection String saved in: appsettings.local.json"
Write-Host ""
Write-Host "Next Steps:"
Write-Host "1. Use appsettings.local.json for local development"
Write-Host "2. Test migration script against local database first"
Write-Host "3. Create kwdb02_local for testing the migration"
Write-Host "==================================================="

# Cleanup
if (Test-Path $BackupPath) {
    $cleanup = Read-Host "`nDelete backup file $BackupPath? (y/n)"
    if ($cleanup -eq 'y') {
        Remove-Item $BackupPath -Force
        Write-Host "✅ Backup file deleted" -ForegroundColor Green
    }
}
