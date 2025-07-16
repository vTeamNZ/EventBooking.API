# PowerShell script to setup URL Rewrite rules for KiwiLanka deployment
# This script must be run as Administrator

Import-Module WebAdministration

$siteName = "kiwilanka.co.nz"

Write-Host "Setting up URL Rewrite rules for $siteName..." -ForegroundColor Green

# Clear existing rewrite rules
Clear-WebConfiguration -Filter "system.webServer/rewrite/rules" -PSPath "IIS:\Sites\$siteName"

# Rule 1: API routes to /api application
Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules" -Name "." -Value @{
    name = "API Routes"
    stopProcessing = $true
}

Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='API Routes']/match" -Name "url" -Value "^api/(.*)"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='API Routes']/action" -Name "type" -Value "Rewrite"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='API Routes']/action" -Name "url" -Value "api/{R:1}"

# Rule 2: QR App routes to /qrapp application
Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules" -Name "." -Value @{
    name = "QR App Routes"
    stopProcessing = $true
}

Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='QR App Routes']/match" -Name "url" -Value "^qrapp/(.*)"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='QR App Routes']/action" -Name "type" -Value "Rewrite"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='QR App Routes']/action" -Name "url" -Value "qrapp/{R:1}"

# Rule 3: QR App API routes to /qrapp-api application
Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules" -Name "." -Value @{
    name = "QR App API Routes"
    stopProcessing = $true
}

Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='QR App API Routes']/match" -Name "url" -Value "^qrapp-api/(.*)"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='QR App API Routes']/action" -Name "type" -Value "Rewrite"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='QR App API Routes']/action" -Name "url" -Value "qrapp-api/{R:1}"

# Rule 4: Default route to frontend (catch-all for SPA)
Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules" -Name "." -Value @{
    name = "Frontend SPA Routes"
    stopProcessing = $true
}

Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='Frontend SPA Routes']/match" -Name "url" -Value ".*"

# Add conditions for the SPA rule (exclude files and API routes)
Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='Frontend SPA Routes']/conditions" -Name "." -Value @{
    input = "{REQUEST_FILENAME}"
    matchType = "IsFile"
    negate = $true
}

Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='Frontend SPA Routes']/conditions" -Name "." -Value @{
    input = "{REQUEST_FILENAME}"
    matchType = "IsDirectory"
    negate = $true
}

Add-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='Frontend SPA Routes']/conditions" -Name "." -Value @{
    input = "{REQUEST_URI}"
    pattern = "^/(api|qrapp|qrapp-api)"
    negate = $true
}

Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='Frontend SPA Routes']/action" -Name "type" -Value "Rewrite"
Set-WebConfigurationProperty -PSPath "IIS:\Sites\$siteName" -Filter "system.webServer/rewrite/rules/rule[@name='Frontend SPA Routes']/action" -Name "url" -Value "index.html"

Write-Host "URL Rewrite rules have been configured successfully!" -ForegroundColor Green

# Display the configured rules
Write-Host "`nConfigured Rules:" -ForegroundColor Yellow
Get-WebConfiguration -Filter "system.webServer/rewrite/rules/rule" -PSPath "IIS:\Sites\$siteName" | ForEach-Object {
    Write-Host "- $($_.name): $($_.match.url) -> $($_.action.url)" -ForegroundColor Cyan
}

Write-Host "`nRestarting application pools..." -ForegroundColor Green
Restart-WebAppPool -Name "KiwiLankaFrontendPool"
Restart-WebAppPool -Name "KiwiLankaBackendAPIPool"
Restart-WebAppPool -Name "KiwiLankaQRAppPool"
Restart-WebAppPool -Name "KiwiLankaQRAppAPIPool"

Write-Host "Setup complete! Your applications should now be accessible at:" -ForegroundColor Green
Write-Host "- Frontend: https://kiwilanka.co.nz" -ForegroundColor White
Write-Host "- API: https://kiwilanka.co.nz/api/*" -ForegroundColor White
Write-Host "- QR App: https://kiwilanka.co.nz/qrapp/*" -ForegroundColor White
Write-Host "- QR App API: https://kiwilanka.co.nz/qrapp-api/*" -ForegroundColor White
