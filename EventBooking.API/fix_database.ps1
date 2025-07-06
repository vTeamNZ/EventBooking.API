# PowerShell script to fix the database schema
# Run this script to add missing columns to the Organizers table

$connectionString = "Server=.\SQLEXPRESS;Database=EventBookingDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"

$sqlScript = @"
-- Add missing columns to Organizers table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
    PRINT 'Added CreatedAt column'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'IsVerified')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [IsVerified] bit NOT NULL DEFAULT 0
    PRINT 'Added IsVerified column'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'OrganizationName')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [OrganizationName] nvarchar(max) NULL
    PRINT 'Added OrganizationName column'
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'Website')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [Website] nvarchar(max) NULL
    PRINT 'Added Website column'
END

PRINT 'Database schema update completed!'
"@

try {
    Write-Host "Connecting to database..." -ForegroundColor Yellow
    
    # Execute the SQL script
    Invoke-Sqlcmd -ConnectionString $connectionString -Query $sqlScript -Verbose
    
    Write-Host "✅ Database schema updated successfully!" -ForegroundColor Green
    Write-Host "The organizer dashboard should now work properly." -ForegroundColor Green
} catch {
    Write-Host "❌ Error updating database: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "You may need to install SqlServer PowerShell module:" -ForegroundColor Yellow
    Write-Host "Install-Module -Name SqlServer -Scope CurrentUser" -ForegroundColor Yellow
}
