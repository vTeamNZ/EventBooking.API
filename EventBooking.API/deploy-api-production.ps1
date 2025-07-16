# EventBooking API Production Deployment Script
# Builds and deploys the .NET API with processing fee functionality

param(
    [switch]$SkipBuild = $false,
    [switch]$SkipBackup = $false,
    [string]$BackupPath = "C:\Backups\EventBookingAPI",
    [string]$PublishPath = ".\publish\production",
    [string]$IISPath = "C:\inetpub\wwwroot\eventbooking-api"  # Adjust this to your production path
)

# Configuration
$ErrorActionPreference = "Stop"
$ProjectPath = ".\EventBooking.API.csproj"
$ProjectName = "EventBooking.API"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "`n=== $Message ===" "Cyan"
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput $Message "Green"
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput $Message "Yellow"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput $Message "Red"
}

try {
    Write-Step "Starting EventBooking API Production Deployment"
    Write-ColorOutput "Timestamp: $Timestamp" "Gray"
    Write-ColorOutput "Project: $ProjectName" "Gray"

    # Check if we're in the right directory
    if (!(Test-Path $ProjectPath)) {
        throw "Project file not found: $ProjectPath. Please run this script from the EventBooking.API directory."
    }

    # Step 1: Backup existing deployment (if not skipped)
    if (!$SkipBackup -and (Test-Path $IISPath)) {
        Write-Step "Creating Backup"
        $BackupDir = "$BackupPath\$Timestamp"
        if (!(Test-Path $BackupPath)) {
            New-Item -ItemType Directory -Path $BackupPath -Force | Out-Null
        }
        Write-ColorOutput "Backing up to: $BackupDir" "Gray"
        Copy-Item -Path $IISPath -Destination $BackupDir -Recurse -Force
        Write-Success "✓ Backup completed"
    } else {
        Write-Warning "⚠ Backup skipped"
    }

    # Step 2: Build the application (if not skipped)
    if (!$SkipBuild) {
        Write-Step "Building Application"
        Write-ColorOutput "Configuration: Release" "Gray"
        
        # Clean previous builds
        if (Test-Path $PublishPath) {
            Remove-Item -Path $PublishPath -Recurse -Force
        }
        
        # Restore packages
        Write-ColorOutput "Restoring NuGet packages..." "Gray"
        dotnet restore $ProjectPath
        if ($LASTEXITCODE -ne 0) { throw "Package restore failed" }
        
        # Build in release mode
        Write-ColorOutput "Building in Release mode..." "Gray"
        dotnet build $ProjectPath --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        
        # Publish the application
        Write-ColorOutput "Publishing application..." "Gray"
        dotnet publish $ProjectPath --configuration Release --output $PublishPath --no-build
        if ($LASTEXITCODE -ne 0) { throw "Publish failed" }
        
        Write-Success "✓ Build completed successfully"
    } else {
        Write-Warning "⚠ Build skipped"
        if (!(Test-Path $PublishPath)) {
            throw "Publish directory not found: $PublishPath. Cannot skip build when no previous build exists."
        }
    }

    # Step 3: Deploy to IIS
    Write-Step "Deploying to Production"
    Write-ColorOutput "Target: $IISPath" "Gray"
    
    # Stop IIS application pool (adjust pool name as needed)
    $AppPoolName = "EventBookingAPI"  # Adjust this to your actual app pool name
    Write-ColorOutput "Stopping application pool: $AppPoolName" "Gray"
    try {
        Import-Module WebAdministration -ErrorAction SilentlyContinue
        if (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) {
            Stop-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 3
        }
    } catch {
        Write-Warning "Could not stop app pool (might not exist or no permissions)"
    }
    
    # Copy files to production
    if (Test-Path $IISPath) {
        Remove-Item -Path "$IISPath\*" -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path $IISPath -Force | Out-Null
    }
    
    Copy-Item -Path "$PublishPath\*" -Destination $IISPath -Recurse -Force
    
    # Start application pool
    Write-ColorOutput "Starting application pool: $AppPoolName" "Gray"
    try {
        if (Get-IISAppPool -Name $AppPoolName -ErrorAction SilentlyContinue) {
            Start-WebAppPool -Name $AppPoolName -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Warning "Could not start app pool"
    }
    
    Write-Success "✓ Deployment completed successfully"

    # Step 4: Database Update Reminder
    Write-Step "Database Update Required"
    Write-Warning "⚠ IMPORTANT: You need to run the database migration for processing fees!"
    Write-ColorOutput "Run this SQL script on your production database:" "Yellow"
    Write-ColorOutput "ALTER TABLE Events ADD ProcessingFeePercentage decimal(5,4) NULL;" "Magenta"
    Write-ColorOutput "ALTER TABLE Events ADD ProcessingFeeFixedAmount decimal(18,2) NULL;" "Magenta"
    Write-ColorOutput "ALTER TABLE Events ADD ProcessingFeeEnabled bit NOT NULL DEFAULT 0;" "Magenta"

    # Step 5: Verification
    Write-Step "Deployment Summary"
    Write-Success "✓ EventBooking API deployed successfully"
    Write-ColorOutput "Deployed to: $IISPath" "Gray"
    Write-ColorOutput "Backup location: $BackupPath\$Timestamp" "Gray"
    Write-ColorOutput "Build artifacts: $PublishPath" "Gray"
    
    Write-Warning "Next steps:"
    Write-ColorOutput "1. Run the database migration script above" "Yellow"
    Write-ColorOutput "2. Test the API endpoints: /api/Events, /api/payment/calculate-processing-fee" "Yellow"
    Write-ColorOutput "3. Verify processing fee functionality in admin panel" "Yellow"
    
} catch {
    Write-Error "❌ Deployment failed: $_"
    Write-ColorOutput "Check the error details above and try again." "Red"
    exit 1
}

Write-Success "`n🎉 Deployment completed! The EventBooking API with processing fee functionality is now live."
