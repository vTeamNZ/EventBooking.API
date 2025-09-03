#!/usr/bin/env powershell

# CHECK PRODUCTION API LOGS AND STATUS
Write-Host "🔍 CHECKING PRODUCTION API STATUS" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan

$prodLogPath = "C:\inetpub\wwwroot\kiwilanka\api\logs"
$prodApiPath = "C:\inetpub\wwwroot\kiwilanka\api"

Write-Host "📂 Production API Path: $prodApiPath" -ForegroundColor Yellow
Write-Host "📄 Production Log Path: $prodLogPath" -ForegroundColor Yellow

# Check if production paths exist
if (Test-Path $prodApiPath) {
    Write-Host "✅ Production API directory exists" -ForegroundColor Green
    
    # Check if OrganizerSalesController.dll exists in production
    $controllerFile = Get-ChildItem -Path $prodApiPath -Recurse -Name "*OrganizerSales*" -ErrorAction SilentlyContinue
    if ($controllerFile) {
        Write-Host "✅ OrganizerSalesController found in production: $controllerFile" -ForegroundColor Green
    } else {
        Write-Host "❌ OrganizerSalesController NOT found in production!" -ForegroundColor Red
    }
    
    # Check main DLL timestamp
    $mainDll = Get-ChildItem -Path $prodApiPath -Name "EventBooking.API.dll" -ErrorAction SilentlyContinue
    if ($mainDll) {
        $dllPath = Join-Path $prodApiPath $mainDll
        $lastWrite = (Get-Item $dllPath).LastWriteTime
        Write-Host "📦 Main DLL last updated: $lastWrite" -ForegroundColor Cyan
    }
} else {
    Write-Host "❌ Production API directory not found!" -ForegroundColor Red
}

# Check production logs
if (Test-Path $prodLogPath) {
    Write-Host "✅ Production log directory exists" -ForegroundColor Green
    
    # Get today's log file
    $today = Get-Date -Format "yyyyMMdd"
    $todayLog = Join-Path $prodLogPath "app-$today.log"
    
    if (Test-Path $todayLog) {
        Write-Host "📋 Today's log file: $todayLog" -ForegroundColor Cyan
        
        # Get recent log entries (last 50 lines)
        Write-Host "🔍 Recent log entries:" -ForegroundColor Yellow
        Get-Content $todayLog -Tail 50 | ForEach-Object {
            if ($_ -match "error|exception|404|500") {
                Write-Host $_ -ForegroundColor Red
            } elseif ($_ -match "organizer|dashboard|sales") {
                Write-Host $_ -ForegroundColor Green
            } else {
                Write-Host $_ -ForegroundColor Gray
            }
        }
        
        # Search for specific organizer-related entries
        Write-Host ""
        Write-Host "🎯 Organizer Dashboard Related Entries:" -ForegroundColor Magenta
        Get-Content $todayLog | Select-String -Pattern "organizer|dashboard|sales|OrganizerSales" -CaseSensitive:$false | Select-Object -Last 10
        
        # Search for 404 errors
        Write-Host ""
        Write-Host "❌ Recent 404 Errors:" -ForegroundColor Red
        Get-Content $todayLog | Select-String -Pattern "404" | Select-Object -Last 5
        
    } else {
        Write-Host "❌ Today's log file not found: $todayLog" -ForegroundColor Red
        
        # List available log files
        Write-Host "📁 Available log files:" -ForegroundColor Yellow
        Get-ChildItem $prodLogPath -Name "*.log" | Sort-Object -Descending | Select-Object -First 5
    }
} else {
    Write-Host "❌ Production log directory not found!" -ForegroundColor Red
}

Write-Host ""
Write-Host "🧪 TEST COMMANDS:" -ForegroundColor Cyan
Write-Host "Test endpoint directly: curl -H 'Authorization: Bearer YOUR_TOKEN' https://kiwilanka.co.nz/api/organizer/dashboard/summary" -ForegroundColor White
Write-Host "Monitor logs live: Get-Content '$prodLogPath\app-$(Get-Date -Format 'yyyyMMdd').log' -Wait" -ForegroundColor White
