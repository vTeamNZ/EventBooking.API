# Entity Framework Migration Best Practices for KiwiLanka

## 🎯 STREAMLINED MIGRATION WORKFLOW

### 1. Pre-Migration Checklist
```bash
# Always run before creating migrations
dotnet ef database update --connection "Server=tcp:kwsqlsvr01.database.windows.net,1433;Initial Catalog=kwdb01;..."
dotnet ef migrations list
```

### 2. Smart Migration Commands
```bash
# Create migration with descriptive name
dotnet ef migrations add Fix_TicketTypes_Name_Column --context AppDbContext

# Preview SQL without applying
dotnet ef migrations script --from 0 --idempotent --output migration-preview.sql

# Apply to specific environment
dotnet ef database update --environment Production
```

### 3. Database Seed Management
Create in `Data/TestDataSeeder.cs`:
```csharp
public static class TestDataSeeder 
{
    public static async Task SeedAsync(AppDbContext context, bool clearExisting = false)
    {
        if (clearExisting)
        {
            // Smart cleanup that preserves users
            context.Seats.RemoveRange(context.Seats);
            context.TicketTypes.RemoveRange(context.TicketTypes);
            context.Events.RemoveRange(context.Events);
            // Don't remove Users or Organizers
        }
        
        await SeedOrganizersAsync(context);
        await SeedVenuesAsync(context);  
        await SeedEventsAsync(context);
        await context.SaveChangesAsync();
    }
}
```

### 4. Environment-Specific Configurations
```json
// appsettings.Development.json
{
  "DatabaseSeeding": {
    "EnableAutoSeed": true,
    "SeedLevel": "Full", // Minimal, Basic, Full
    "ClearExistingData": true
  }
}

// appsettings.Production.json  
{
  "DatabaseSeeding": {
    "EnableAutoSeed": false,
    "SeedLevel": "Minimal",
    "ClearExistingData": false
  }
}
```

### 5. Startup Configuration
```csharp
// In Program.cs
if (app.Environment.IsDevelopment())
{
    var config = app.Configuration.GetSection("DatabaseSeeding");
    if (config.GetValue<bool>("EnableAutoSeed"))
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TestDataSeeder.SeedAsync(context, 
            config.GetValue<bool>("ClearExistingData"));
    }
}
```

## 🚀 RECOMMENDED WORKFLOW FOR FUTURE UPDATES

### Scenario 1: Schema Changes
```bash
# 1. Make model changes in code
# 2. Create migration
dotnet ef migrations add YourChangeName

# 3. Test locally first
dotnet ef database update --connection "LocalConnectionString"

# 4. Generate production script
dotnet ef migrations script --idempotent --output prod-migration.sql

# 5. Review script, then apply to Azure
sqlcmd -S kwsqlsvr01.database.windows.net -d kwdb01 -U gayantd -P password -i prod-migration.sql
```

### Scenario 2: Data Updates Only
```powershell
# Use our new manager
.\db-manager.ps1 -Action quick    # Fix schema + minimal data
.\db-manager.ps1 -Action setup    # Full data refresh
```

### Scenario 3: Emergency Fix
```sql
-- Use stored procedures
EXEC sp_HealthCheck        -- Diagnose issues
EXEC sp_FixSchema          -- Auto-fix common problems
EXEC sp_SetupDatabase 'QUICK'  -- Complete fix
```

## 📊 MONITORING & VALIDATION

### Health Check Endpoint
Create `Controllers/HealthController.cs`:
```csharp
[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _context;
    
    [HttpGet("database")]
    public async Task<IActionResult> CheckDatabase()
    {
        try {
            var counts = new {
                Events = await _context.Events.CountAsync(),
                Users = await _context.Users.CountAsync(), 
                Venues = await _context.Venues.CountAsync()
            };
            return Ok(new { Status = "Healthy", Data = counts });
        }
        catch (Exception ex) {
            return StatusCode(500, new { Status = "Unhealthy", Error = ex.Message });
        }
    }
}
```

### Automated Testing
```bash
# Test endpoints after changes
curl https://kiwilanka.co.nz/api/Health/database
curl https://kiwilanka.co.nz/api/Events
```

## 🔧 TROUBLESHOOTING GUIDE

### Common Issues & Quick Fixes
1. **"Invalid column name"** → Run `EXEC sp_FixSchema`
2. **Empty API responses** → Run `EXEC sp_SetupDatabase 'QUICK'`
3. **Migration conflicts** → Use `--force` flag or manual merge
4. **Foreign key errors** → Check user/organizer relationships

### Emergency Commands
```sql
-- Quick diagnosis
SELECT 'Events', COUNT(*) FROM Events
UNION ALL SELECT 'Users', COUNT(*) FROM AspNetUsers;

-- Quick fix
EXEC sp_SetupDatabase 'QUICK';
```

This approach gives you:
✅ One-command database fixes
✅ Automated schema validation  
✅ Smart data seeding
✅ Environment-specific configs
✅ Built-in health checks
✅ Emergency recovery procedures
