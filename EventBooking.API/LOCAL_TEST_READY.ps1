# =====================================================
# CONNECTION STRING BACKUP & QUICK TEST SCRIPT
# =====================================================

Write-Host "==================================================="
Write-Host "🔗 CONNECTION STRING UPDATED FOR LOCAL TESTING" -ForegroundColor Green
Write-Host "==================================================="

Write-Host "`n📋 Changes Made:" -ForegroundColor Yellow
Write-Host "✅ Updated appsettings.json to use local SQL Express"
Write-Host "✅ Database: kwdb01_local"
Write-Host "✅ Server: .\SQLEXPRESS"

Write-Host "`n🔄 Original Azure Connection (for backup):" -ForegroundColor Cyan
Write-Host "Server=tcp:kwsqlsvr01.database.windows.net,1433;Initial Catalog=kwdb01;Persist Security Info=False;User ID=gayantd;Password=maGulak@143456;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=120;Command Timeout=300;"

Write-Host "`n🏠 New Local Connection:" -ForegroundColor Green
Write-Host "Server=.\SQLEXPRESS;Database=kwdb01_local;Integrated Security=true;MultipleActiveResultSets=False;Encrypt=False;TrustServerCertificate=True;Connection Timeout=120;Command Timeout=300;"

Write-Host "`n🚀 Ready to Test Your Application!" -ForegroundColor Magenta
Write-Host "=================================="

Write-Host "`n📝 Quick Test Commands:" -ForegroundColor Yellow
Write-Host "1. Build the application:"
Write-Host "   dotnet build"
Write-Host ""
Write-Host "2. Run the application:"
Write-Host "   dotnet run"
Write-Host ""
Write-Host "3. Test endpoints:"
Write-Host "   https://localhost:5000/swagger"
Write-Host "   https://localhost:5000/api/events"

Write-Host "`n🔧 To Revert Back to Azure:" -ForegroundColor Cyan
Write-Host "Replace the connection string in appsettings.json with:"
Write-Host "Server=tcp:kwsqlsvr01.database.windows.net,1433;Initial Catalog=kwdb01;..."

Write-Host "`n⚠️  Note:" -ForegroundColor Yellow
Write-Host "- Your local database has the basic schema but may need data"
Write-Host "- Use BCP commands to import specific data if needed"
Write-Host "- Test core functionality like Events, Users, Bookings"

Write-Host "`n✅ Ready to start local development!" -ForegroundColor Green
