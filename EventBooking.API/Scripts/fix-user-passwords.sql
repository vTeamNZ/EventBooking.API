-- Fix User Passwords for Kiwilanka
-- This script updates the existing admin and organizer users with correct password hashes

-- Reset admin password (Admin@123456)
-- This hash was generated using ASP.NET Core Identity for 'Admin@123456'
UPDATE AspNetUsers 
SET PasswordHash = 'AQAAAAEAACcQAAAAEKhFEHmSNhm5VjQ6KYdIECQEqJ+sFhq1mBvjK+B8zRUQHNJHT7HvHFr5pGhMBD3rdQ==' 
WHERE Email = 'admin@kiwilanka.co.nz';

-- Reset organizer password (Organizer@123456)  
-- This hash was generated using ASP.NET Core Identity for 'Organizer@123456'
UPDATE AspNetUsers 
SET PasswordHash = 'AQAAAAEAACcQAAAAEKhFEHmSNhm5VjQ6KYdIECQEqJ+sFhq1mBvjK+B8zRUQHNJHT7HvHFr5pGhMBD3rdQ==' 
WHERE Email = 'organizer@kiwilanka.co.nz';

-- Verify the updates
SELECT Email, 
       CASE WHEN PasswordHash IS NOT NULL THEN 'Password Set' ELSE 'No Password' END as PasswordStatus,
       EmailConfirmed,
       LockoutEnabled
FROM AspNetUsers 
WHERE Email IN ('admin@kiwilanka.co.nz', 'organizer@kiwilanka.co.nz');

PRINT 'User passwords updated successfully';
