-- Fix Organizers table by adding missing columns
-- Run this script in your SQL Server Management Studio or SQL command line

USE [EventBookingDb] -- Replace with your actual database name
GO

-- Add missing columns to Organizers table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'CreatedAt')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE()
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'IsVerified')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [IsVerified] bit NOT NULL DEFAULT 0
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'OrganizationName')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [OrganizationName] nvarchar(max) NULL
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Organizers]') AND name = 'Website')
BEGIN
    ALTER TABLE [dbo].[Organizers] ADD [Website] nvarchar(max) NULL
END

PRINT 'Organizers table updated successfully'
GO
