-- Fix Authentication Issue - Recreate Admin and Organizer Users
-- This script removes the existing users and lets the seeder recreate them with correct passwords

BEGIN TRANSACTION;

-- Remove existing admin user from roles
DELETE FROM AspNetUserRoles 
WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email = 'admin@kiwilanka.co.nz');

-- Remove existing organizer user from roles  
DELETE FROM AspNetUserRoles 
WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email = 'organizer@kiwilanka.co.nz');

-- Remove organizer records that depend on the user
DELETE FROM Organizers 
WHERE UserId IN (SELECT Id FROM AspNetUsers WHERE Email = 'organizer@kiwilanka.co.nz');

-- Remove the users themselves
DELETE FROM AspNetUsers WHERE Email = 'admin@kiwilanka.co.nz';
DELETE FROM AspNetUsers WHERE Email = 'organizer@kiwilanka.co.nz';

-- Verify deletion
SELECT 'Users removed' as Status;
SELECT COUNT(*) as RemainingUsers FROM AspNetUsers;

COMMIT TRANSACTION;

PRINT 'Users removed successfully. Restart the API to trigger the seeder.';
