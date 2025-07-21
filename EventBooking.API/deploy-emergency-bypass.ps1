# Emergency Bypass Deployment Script
# This deploys the emergency bypass solution to help users complete bookings

Write-Host "🚨 Deploying Emergency Bypass Solution..." -ForegroundColor Yellow

# 1. Build the API with emergency bypass endpoint
Write-Host "Building API with emergency bypass endpoint..." -ForegroundColor Green
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

# 2. Publish to production folder
Write-Host "Publishing to production..." -ForegroundColor Green
dotnet publish --configuration Release --output "bin\Release\publish"

# 3. Copy emergency booking HTML to wwwroot
Write-Host "Copying emergency booking tool..." -ForegroundColor Green
$wwwrootPath = "bin\Release\publish\wwwroot"
if (!(Test-Path $wwwrootPath)) {
    New-Item -ItemType Directory -Path $wwwrootPath -Force
}
Copy-Item "emergency-booking.html" "$wwwrootPath\emergency-booking.html" -Force

Write-Host "✅ Emergency bypass solution ready for deployment!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Manual Steps Required:" -ForegroundColor Cyan
Write-Host "1. Stop IIS/Application Pool"
Write-Host "2. Copy contents of 'bin\Release\publish' to production server"
Write-Host "3. Update appsettings.Production.json with Azure SQL connection"
Write-Host "4. Start IIS/Application Pool"
Write-Host "5. Test emergency endpoint: https://kiwilanka.co.nz/api/Seats/emergency-bypass-reservation"
Write-Host "6. Share emergency tool: https://kiwilanka.co.nz/emergency-booking.html"
Write-Host ""
Write-Host "🔧 Emergency Booking Tool provides users a way to bypass the broken seat selection flow"
Write-Host "Users can access it directly at: https://kiwilanka.co.nz/emergency-booking.html"
