-- =====================================================
-- SIMPLIFIED LOCAL DATABASE MIGRATION
-- =====================================================

-- Create local database and all essential tables
USE master;
GO

-- Drop and create fresh database
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'kwdb01_local')
BEGIN
    ALTER DATABASE [kwdb01_local] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [kwdb01_local];
END
GO

CREATE DATABASE [kwdb01_local];
GO

USE [kwdb01_local];
GO

-- Create AspNetUsers table
CREATE TABLE [dbo].[AspNetUsers] (
    [Id] nvarchar(450) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [UserName] nvarchar(256) NULL,
    [NormalizedUserName] nvarchar(256) NULL,
    [Email] nvarchar(256) NULL,
    [NormalizedEmail] nvarchar(256) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL,
    CONSTRAINT [PK_AspNetUsers] PRIMARY KEY ([Id])
);

-- Create Events table
CREATE TABLE [dbo].[Events] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Date] datetime2 NULL,
    [Location] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NULL,
    [Capacity] int NULL,
    [OrganizerId] int NULL,
    [ImageUrl] nvarchar(max) NULL,
    [IsActive] bit NOT NULL,
    [SeatSelectionMode] int NOT NULL,
    [StagePosition] nvarchar(max) NULL,
    [VenueId] int NULL,
    CONSTRAINT [PK_Events] PRIMARY KEY ([Id])
);

-- Create Bookings table
CREATE TABLE [dbo].[Bookings] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [EventId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaymentIntentId] nvarchar(max) NULL,
    CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bookings_Events] FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create Payments table
CREATE TABLE [dbo].[Payments] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [PaymentIntentId] nvarchar(510) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(20) NOT NULL,
    [Status] nvarchar(100) NOT NULL,
    [EventId] int NOT NULL,
    [TicketDetails] nvarchar(max) NOT NULL,
    [FoodDetails] nvarchar(max) NOT NULL,
    [UpdatedAt] datetime2 NULL,
    [Description] nvarchar(1000) NOT NULL,
    [Email] nvarchar(510) NOT NULL,
    [FirstName] nvarchar(200) NOT NULL,
    [LastName] nvarchar(200) NOT NULL,
    [Mobile] nvarchar(100) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id])
);

-- Create TicketTypes table  
CREATE TABLE [dbo].[TicketTypes] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Description] nvarchar(max) NULL,
    [EventId] int NOT NULL,
    [SeatRowAssignments] nvarchar(max) NULL,
    [Color] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NULL,
    CONSTRAINT [PK_TicketTypes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TicketTypes_Events] FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create BookingTickets table
CREATE TABLE [dbo].[BookingTickets] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [BookingId] int NOT NULL,
    [TicketTypeId] int NOT NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_BookingTickets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BookingTickets_Bookings] FOREIGN KEY ([BookingId]) REFERENCES [Bookings]([Id]),
    CONSTRAINT [FK_BookingTickets_TicketTypes] FOREIGN KEY ([TicketTypeId]) REFERENCES [TicketTypes]([Id])
);

PRINT 'Essential tables created successfully for local database';
