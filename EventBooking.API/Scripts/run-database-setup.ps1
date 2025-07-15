# PowerShell script to execute the complete database setup
# This script will run the SQL script against your Azure SQL database

param(
    [string]$ServerName = "kwsqlsvr01.database.windows.net",
    [string]$DatabaseName = "kwdb01",
    [string]$Username = "gayantd",
    [string]$Password = "maGulak@143456"
)

# Import SQL Server module if available
if (Get-Module -ListAvailable -Name SqlServer) {
    Import-Module SqlServer
    Write-Host "Using SqlServer PowerShell module" -ForegroundColor Green
    
    # Connection string
    $connectionString = "Server=tcp:$ServerName,1433;Initial Catalog=$DatabaseName;Persist Security Info=False;User ID=$Username;Password=$Password;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
    
    # Path to SQL script
    $scriptPath = Join-Path $PSScriptRoot "complete-database-setup.sql"
    
    if (Test-Path $scriptPath) {
        Write-Host "Executing database setup script..." -ForegroundColor Yellow
        
        try {
            # Execute the SQL script
            Invoke-Sqlcmd -ConnectionString $connectionString -InputFile $scriptPath -Verbose
            Write-Host "Database setup completed successfully!" -ForegroundColor Green
        }
        catch {
            Write-Error "Error executing SQL script: $($_.Exception.Message)"
            exit 1
        }
    }
    else {
        Write-Error "SQL script file not found at: $scriptPath"
        exit 1
    }
}
else {
    Write-Host "SqlServer module not available. Using sqlcmd..." -ForegroundColor Yellow
    
    # Path to SQL script
    $scriptPath = Join-Path $PSScriptRoot "complete-database-setup.sql"
    
    if (Test-Path $scriptPath) {
        Write-Host "Executing database setup script using sqlcmd..." -ForegroundColor Yellow
        
        try {
            # Use sqlcmd if available
            $sqlcmdArgs = @(
                "-S", "$ServerName,1433"
                "-d", $DatabaseName
                "-U", $Username
                "-P", $Password
                "-i", $scriptPath
                "-b"  # Exit batch on error
            )
            
            & sqlcmd @sqlcmdArgs
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Database setup completed successfully!" -ForegroundColor Green
            } else {
                Write-Error "sqlcmd execution failed with exit code: $LASTEXITCODE"
                exit 1
            }
        }
        catch {
            Write-Error "Error executing sqlcmd: $($_.Exception.Message)"
            Write-Host "Please install SQL Server Command Line Utilities or SQL Server PowerShell module" -ForegroundColor Yellow
            Write-Host "Download from: https://docs.microsoft.com/en-us/sql/tools/sqlcmd-utility" -ForegroundColor Yellow
            exit 1
        }
    }
    else {
        Write-Error "SQL script file not found at: $scriptPath"
        exit 1
    }
}

Write-Host ""
Write-Host "Database setup completed! You can now test your API endpoints:" -ForegroundColor Cyan
Write-Host "- https://kiwilanka.co.nz/api/Events" -ForegroundColor White
Write-Host "- https://kiwilanka.co.nz/api/Venues" -ForegroundColor White
Write-Host "- https://kiwilanka.co.nz/api/Organizers" -ForegroundColor White
