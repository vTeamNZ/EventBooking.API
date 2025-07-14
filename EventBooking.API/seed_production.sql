USE [kwdb01]
GO
SET QUOTED_IDENTIFIER ON
GO
SET ANSI_NULLS ON
GO

-- Check if roles exist
IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Admin')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Admin', 'ADMIN', NEWID())
END

IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'User')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'User', 'USER', NEWID())
END

IF NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = 'Organizer')
BEGIN
    INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
    VALUES (NEWID(), 'Organizer', 'ORGANIZER', NEWID())
END

-- Check if admin user exists
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Email = 'admin@kiwilanka.co.nz')
BEGIN
    DECLARE @AdminId NVARCHAR(450) = NEWID()
    DECLARE @AdminRoleId NVARCHAR(450) = (SELECT Id FROM AspNetRoles WHERE Name = 'Admin')
    
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FullName, Role)
    VALUES (@AdminId, 'admin@kiwilanka.co.nz', 'ADMIN@KIWILANKA.CO.NZ', 'admin@kiwilanka.co.nz', 'ADMIN@KIWILANKA.CO.NZ', 1,
    'AQAAAAEAACcQAAAAEHxrxS6vVW6v+dTo9YQFN/yAg9yElAxkHyxEOz69wZ5wkzMzXH5MHKji/tsEOS0Wpw==', -- This is 'Admin@123456' hashed
    NEWID(), NEWID(), 0, 0, 1, 0, 'System Administrator', 'Admin')

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@AdminId, @AdminRoleId)
END

-- Check if organizer user exists
IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Email = 'organizer@kiwilanka.co.nz')
BEGIN
    DECLARE @OrganizerId NVARCHAR(450) = NEWID()
    DECLARE @OrganizerRoleId NVARCHAR(450) = (SELECT Id FROM AspNetRoles WHERE Name = 'Organizer')
    
    INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, FullName, Role)
    VALUES (@OrganizerId, 'organizer@kiwilanka.co.nz', 'ORGANIZER@KIWILANKA.CO.NZ', 'organizer@kiwilanka.co.nz', 'ORGANIZER@KIWILANKA.CO.NZ', 1,
    'AQAAAAEAACcQAAAAEHxrxS6vVW6v+dTo9YQFN/yAg9yElAxkHyxEOz69wZ5wkzMzXH5MHKji/tsEOS0Wpw==', -- This is 'Organizer@123456' hashed
    NEWID(), NEWID(), 0, 0, 1, 0, 'Test Organizer', 'Organizer')

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@OrganizerId, @OrganizerRoleId)
END
