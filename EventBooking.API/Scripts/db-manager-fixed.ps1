# KiwiLanka Database Management PowerShell Module
# Provides easy commands for database operations

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("health", "fix", "setup", "reset", "quick")]
    [string]$Action = "health",
    
    [Parameter(Mandatory=$false)]
    [string]$ServerName = "kwsqlsvr01.database.windows.net",
    
    [Parameter(Mandatory=$false)]
    [string]$DatabaseName = "kwdb01",
    
    [Parameter(Mandatory=$false)]
    [string]$Username = "gayantd",
    
    [Parameter(Mandatory=$false)]
    [string]$Password = "maGulak@143456"
)

# Connection string
$connectionString = "Server=tcp:$ServerName,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$Username;Password=$Password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

function Invoke-DatabaseCommand {
    param([string]$Command)
    
    try {
        if (Get-Module -ListAvailable -Name SqlServer) {
            Import-Module SqlServer -Force
            Invoke-Sqlcmd -ConnectionString $connectionString -Query $Command -Verbose
        } else {
            # Fallback to sqlcmd
            $tempFile = [System.IO.Path]::GetTempFileName() + ".sql"
            $Command | Out-File -FilePath $tempFile -Encoding UTF8
            & sqlcmd -S "$ServerName,1433" -d $DatabaseName -U $Username -P $Password -i $tempFile
            Remove-Item $tempFile -Force
        }
        return $true
    }
    catch {
        Write-Error "Database command failed: $($_.Exception.Message)"
        return $false
    }
}

function Test-DatabaseHealth {
    Write-Host "🔍 Checking database health..." -ForegroundColor Cyan
    $result = Invoke-DatabaseCommand "EXEC sp_HealthCheck"
    if ($result) {
        Write-Host "✅ Health check completed" -ForegroundColor Green
    }
}

function Repair-DatabaseSchema {
    Write-Host "🔧 Fixing database schema..." -ForegroundColor Yellow
    $result = Invoke-DatabaseCommand "EXEC sp_FixSchema"
    if ($result) {
        Write-Host "✅ Schema fixes applied" -ForegroundColor Green
    }
}

function Initialize-Database {
    param([string]$Mode = "QUICK")
    Write-Host "🚀 Setting up database (Mode: $Mode)..." -ForegroundColor Cyan
    $result = Invoke-DatabaseCommand "EXEC sp_SetupDatabase '$Mode'"
    if ($result) {
        Write-Host "✅ Database setup completed!" -ForegroundColor Green
        Write-Host "🌐 Test API: https://kiwilanka.co.nz/api/Events" -ForegroundColor White
    }
}

function Test-ApiEndpoint {
    Write-Host "🌐 Testing API endpoint..." -ForegroundColor Cyan
    try {
        $response = Invoke-WebRequest -Uri "https://kiwilanka.co.nz/api/Events" -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            $data = $response.Content | ConvertFrom-Json
            Write-Host "✅ API working! Found $($data.Length) events" -ForegroundColor Green
            return $true
        }
    }
    catch {
        Write-Host "❌ API test failed: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

function Install-DatabaseManagement {
    Write-Host "📦 Installing Database Management System..." -ForegroundColor Cyan
    $scriptPath = Join-Path $PSScriptRoot "database-management-system.sql"
    
    if (Test-Path $scriptPath) {
        try {
            if (Get-Module -ListAvailable -Name SqlServer) {
                Import-Module SqlServer -Force
                Invoke-Sqlcmd -ConnectionString $connectionString -InputFile $scriptPath -Verbose
            } else {
                & sqlcmd -S "$ServerName,1433" -d $DatabaseName -U $Username -P $Password -i $scriptPath
            }
            Write-Host "✅ Database Management System installed!" -ForegroundColor Green
        }
        catch {
            Write-Error "Failed to install management system: $($_.Exception.Message)"
        }
    } else {
        Write-Error "Management system SQL file not found at: $scriptPath"
    }
}

# Main execution logic
Write-Host "🔧 KiwiLanka Database Manager" -ForegroundColor Magenta
Write-Host "Action: $Action" -ForegroundColor White

switch ($Action.ToLower()) {
    "health" {
        Test-DatabaseHealth
        Test-ApiEndpoint
    }
    "fix" {
        Repair-DatabaseSchema
        Test-DatabaseHealth
    }
    "setup" {
        Install-DatabaseManagement
        Initialize-Database "FULL"
        Test-ApiEndpoint
    }
    "reset" {
        Install-DatabaseManagement
        Initialize-Database "RESET"
        Test-ApiEndpoint
    }
    "quick" {
        Install-DatabaseManagement
        Initialize-Database "QUICK"
        Test-ApiEndpoint
    }
    default {
        Write-Host "❌ Unknown action: $Action" -ForegroundColor Red
        Write-Host "Available actions: health, fix, setup, reset, quick" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "💡 Usage examples:" -ForegroundColor Cyan
Write-Host "  .\db-manager.ps1 -Action health    # Check status" -ForegroundColor White
Write-Host "  .\db-manager.ps1 -Action quick     # Quick fix & minimal data" -ForegroundColor White
Write-Host "  .\db-manager.ps1 -Action setup     # Full setup with all data" -ForegroundColor White
Write-Host "  .\db-manager.ps1 -Action reset     # Reset everything" -ForegroundColor White
