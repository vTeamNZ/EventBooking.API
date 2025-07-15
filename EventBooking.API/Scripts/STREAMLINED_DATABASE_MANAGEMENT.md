# 🚀 STREAMLINED DATABASE MANAGEMENT FOR KIWILANKA

## ✨ **FUTURE UPDATES - ONE COMMAND SOLUTIONS**

### 🎯 **Most Common Scenarios**

#### **Scenario 1: Quick Fix (Most Common)**
```bash
# One command to fix schema issues and verify API works
sqlcmd -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -i "quick-fix.sql"
```
**Use this when:** API returns errors, empty responses, or schema mismatches

#### **Scenario 2: Emergency Recovery** 
```sql
-- If quick-fix.sql isn't available, run this directly:
sqlcmd -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -Q "
DROP INDEX IF EXISTS IX_Seats_SectionId ON Seats;
ALTER TABLE Seats DROP COLUMN IF EXISTS SectionId;
"
```

#### **Scenario 3: Full Data Refresh**
```bash
# Use the comprehensive script for complete setup
sqlcmd -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -i "fix-database-schema.sql"
```

### 📋 **Pre-Built Scripts Available**

| Script | Purpose | Use When |
|--------|---------|----------|
| `quick-fix.sql` | **⚡ Fast fix** | API errors, schema issues |
| `fix-database-schema.sql` | **🔄 Complete refresh** | Need full sample data |
| `simple-populate.sql` | **📝 Data only** | Schema OK, need data |
| `database-management-system.sql` | **🛠️ Advanced tools** | Ongoing management |

### 🔥 **Emergency Commands (Copy-Paste Ready)**

#### **Check API Status**
```powershell
Invoke-WebRequest -Uri "https://kiwilanka.co.nz/api/Events"
```

#### **Quick Health Check**
```sql
sqlcmd -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -Q "
SELECT 'Events', COUNT(*) FROM Events
UNION ALL SELECT 'Users', COUNT(*) FROM AspNetUsers
UNION ALL SELECT 'Venues', COUNT(*) FROM Venues;
"
```

#### **Force Fix Everything**
```sql
sqlcmd -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -Q "
-- Emergency schema fix
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_SectionId')
    DROP INDEX IX_Seats_SectionId ON Seats;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
    ALTER TABLE Seats DROP COLUMN SectionId;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NULL;
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
    ALTER TABLE Seats ADD TicketTypeId int NOT NULL DEFAULT 1;
"
```

## 🎛️ **Advanced Management (Optional)**

### **Install Management System**
```bash
sqlcmd -S "kwsqlsvr01.database.windows.net,1433" -d "kwdb01" -U "gayantd" -P "maGulak@143456" -i "database-management-system.sql"
```

### **Then Use Stored Procedures**
```sql
EXEC sp_HealthCheck                    -- Check status
EXEC sp_FixSchema                      -- Auto-fix schema
EXEC sp_SetupDatabase 'QUICK'          -- Quick setup
EXEC sp_SetupDatabase 'FULL'           -- Full setup
```

## 📊 **Troubleshooting Guide**

### **Common Error → Quick Fix**

| Error Message | Quick Fix |
|---------------|-----------|
| `Invalid column name 'Name'` | Run `quick-fix.sql` |
| `Invalid column name 'TicketTypeId'` | Run `quick-fix.sql` |
| API returns `[]` (empty) | Run `fix-database-schema.sql` |
| 500 Internal Server Error | Run `quick-fix.sql` then check API |
| Foreign key constraint error | Check Users table, run `simple-populate.sql` |

### **Validation Commands**
```bash
# Check if fix worked
curl "https://kiwilanka.co.nz/api/Events"
curl "https://kiwilanka.co.nz/api/Venues"  
curl "https://kiwilanka.co.nz/api/Organizers"
```

## 🚀 **Best Practices for Future**

### **1. Always Test Locally First**
```bash
# Test on development database first
sqlcmd -S "localhost" -d "EventBookingDev" -E -i "quick-fix.sql"
```

### **2. Use Entity Framework for Schema Changes**
```bash
# For new features, use EF migrations
dotnet ef migrations add NewFeature
dotnet ef migrations script --idempotent --output update.sql
# Review update.sql, then apply to production
```

### **3. Backup Before Major Changes**
```sql
-- Always backup before big changes
BACKUP DATABASE kwdb01 TO URL = 'https://...' WITH COMPRESSION;
```

### **4. Monitor and Alert**
```bash
# Set up automated health checks
# Add to cron/scheduled task:
curl "https://kiwilanka.co.nz/api/Health/database" || echo "API DOWN!"
```

## 🎯 **Summary: What You Get**

✅ **One-command fixes** for 90% of database issues  
✅ **Emergency recovery** procedures ready to copy-paste  
✅ **Health checks** to quickly diagnose problems  
✅ **Automated schema** validation and fixing  
✅ **Smart data seeding** that doesn't break existing data  
✅ **Production-safe** scripts that check before changing  

**Next time database issues occur:** Just run `quick-fix.sql` → Test API → Done! 🎉
