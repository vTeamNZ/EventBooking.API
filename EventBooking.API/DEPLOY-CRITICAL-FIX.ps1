#!/usr/bin/env powershell

# CRITICAL FIX DEPLOYMENT - Ticket/Food Assignment Bug
Write-Host "🎯 DEPLOYING CRITICAL TICKET ASSIGNMENT FIX" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

Write-Host "📁 Copying built files to production..." -ForegroundColor Yellow
Copy-Item -Path "bin\Debug\net8.0\*" -Destination "C:\inetpub\wwwroot\thelankanspace.co.nz\kw\api\" -Recurse -Force

Write-Host "🔄 Restarting IIS Application Pool..." -ForegroundColor Yellow
Import-Module WebAdministration -ErrorAction SilentlyContinue
if (Get-Module WebAdministration) {
    Restart-WebAppPool -Name "DefaultAppPool"
    Write-Host "✅ IIS Application Pool restarted" -ForegroundColor Green
} else {
    Write-Host "⚠️ Please manually restart IIS Application Pool" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "🎯 CRITICAL FIX DEPLOYED" -ForegroundColor Green
Write-Host "========================" -ForegroundColor Green
Write-Host "Fixed Issues:" -ForegroundColor White
Write-Host "• QR tickets now match specific seats/tickets correctly" -ForegroundColor White
Write-Host "• Email attachments will show correct tickets for each seat" -ForegroundColor White
Write-Host "• Food orders remain correctly assigned per seat" -ForegroundColor White

Write-Host ""
Write-Host "🧪 TEST NOW:" -ForegroundColor Cyan
Write-Host "1. Book 2+ seats with different food items per seat" -ForegroundColor White
Write-Host "2. Check email attachments match the correct seats" -ForegroundColor White
Write-Host "3. Verify food items in PDFs match seat assignments" -ForegroundColor White

Write-Host ""
Write-Host "📊 MONITOR LOGS:" -ForegroundColor Yellow
Write-Host "Get-Content 'C:\inetpub\wwwroot\thelankanspace.co.nz\kw\api\logs\app-$(Get-Date -Format 'yyyyMMdd').log' -Wait | Select-String 'ALLOCATED SEATING|GENERAL ADMISSION'" -ForegroundColor Gray
