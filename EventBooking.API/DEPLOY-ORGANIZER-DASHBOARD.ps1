#!/usr/bin/env powershell

# ORGANIZER DASHBOARD FEATURE DEPLOYMENT
Write-Host "📊 DEPLOYING ORGANIZER DASHBOARD FEATURE" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

Write-Host "🔨 Building the application..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed! Please fix compilation errors first." -ForegroundColor Red
    exit 1
}

Write-Host "✅ Build successful!" -ForegroundColor Green

Write-Host "📁 Copying built files to production (kiwilanka.co.nz)..." -ForegroundColor Yellow
Copy-Item -Path "bin\Release\net8.0\*" -Destination "C:\inetpub\wwwroot\kiwilanka\api\" -Recurse -Force

Write-Host "🔄 Restarting IIS Application Pool..." -ForegroundColor Yellow
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Get-Module WebAdministration) {
    Restart-WebAppPool -Name "DefaultAppPool"
    Write-Host "✅ IIS Application Pool restarted" -ForegroundColor Green
} else {
    Write-Host "⚠️ Please manually restart IIS Application Pool" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "📊 ORGANIZER DASHBOARD DEPLOYED" -ForegroundColor Green
Write-Host "===============================" -ForegroundColor Green
Write-Host "New Features:" -ForegroundColor White
Write-Host "• OrganizerSalesController with dashboard summary endpoint" -ForegroundColor White
Write-Host "• Daily analytics endpoint for real-time charts" -ForegroundColor White
Write-Host "• Event bookings endpoint with search/filter" -ForegroundColor White
Write-Host "• Enhanced DTOs for structured data transfer" -ForegroundColor White

Write-Host ""
Write-Host "🧪 TEST ENDPOINTS:" -ForegroundColor Cyan
Write-Host "• GET /api/organizer/dashboard/summary" -ForegroundColor White
Write-Host "• GET /api/organizer/events/{id}/daily-analytics" -ForegroundColor White
Write-Host "• GET /api/organizer/events/{id}/bookings" -ForegroundColor White

Write-Host ""
Write-Host "📊 MONITOR LOGS:" -ForegroundColor Yellow
Write-Host "Get-Content 'C:\inetpub\wwwroot\kiwilanka\api\logs\app-`$(Get-Date -Format 'yyyyMMdd').log' -Wait | Select-String 'OrganizerSales'" -ForegroundColor Gray

Write-Host ""
Write-Host "🌐 Frontend Access:" -ForegroundColor Magenta
Write-Host "https://kiwilanka.co.nz/organizer/sales-dashboard" -ForegroundColor White
