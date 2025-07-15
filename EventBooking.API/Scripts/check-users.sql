-- Check existing users in the database
USE kwdb01;
GO

PRINT 'Checking existing users in AspNetUsers table...';

-- Show user count and basic user info
SELECT COUNT(*) as TotalUsers FROM AspNetUsers;

-- Show detailed user information (without sensitive data)
SELECT 
    Id,
    FullName,
    Role,
    UserName,
    Email,
    EmailConfirmed,
    PhoneNumber,
    PhoneNumberConfirmed,
    TwoFactorEnabled,
    LockoutEnabled,
    AccessFailedCount
FROM AspNetUsers
ORDER BY FullName;

-- Check if there are any roles defined
PRINT 'Checking AspNetRoles...';
SELECT Id, Name, NormalizedName FROM AspNetRoles;

-- Check user role assignments
PRINT 'Checking user role assignments...';
SELECT 
    ur.UserId,
    u.FullName,
    u.Email,
    r.Name as RoleName
FROM AspNetUserRoles ur
INNER JOIN AspNetUsers u ON ur.UserId = u.Id
INNER JOIN AspNetRoles r ON ur.RoleId = r.Id;

-- Check if organizers are linked to users
PRINT 'Checking Organizers linked to users...';
SELECT 
    o.Id as OrganizerId,
    o.Name as OrganizerName,
    o.ContactEmail,
    o.UserId,
    u.FullName,
    u.Email as UserEmail
FROM Organizers o
LEFT JOIN AspNetUsers u ON o.UserId = u.Id;
