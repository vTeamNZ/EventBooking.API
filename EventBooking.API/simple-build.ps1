# Simple Production Build Script
Write-Host "Building EventBooking.API for Production..." -ForegroundColor Green

# Build the application
Write-Host "Running dotnet build..." -ForegroundColor Cyan
dotnet build --configuration Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Publish the application
    Write-Host "Publishing application..." -ForegroundColor Cyan
    dotnet publish --configuration Release --output publish
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Publish successful!" -ForegroundColor Green
        Write-Host "Files ready in ./publish directory" -ForegroundColor White
    } else {
        Write-Host "Publish failed!" -ForegroundColor Red
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
}
