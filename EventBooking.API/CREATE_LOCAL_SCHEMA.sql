-- =====================================================
-- LOCAL DATABASE SCHEMA CREATION SCRIPT
-- Based on the database structure from kwdb01
-- =====================================================

USE [kwdb01_local];
GO

-- Create AspNetUsers table
CREATE TABLE [AspNetUsers] (
    [Id] nvarchar(900) NOT NULL PRIMARY KEY,
    [FullName] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [UserName] nvarchar(512) NULL,
    [NormalizedUserName] nvarchar(512) NULL,
    [Email] nvarchar(512) NULL,
    [NormalizedEmail] nvarchar(512) NULL,
    [EmailConfirmed] bit NOT NULL,
    [PasswordHash] nvarchar(max) NULL,
    [SecurityStamp] nvarchar(max) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    [PhoneNumber] nvarchar(max) NULL,
    [PhoneNumberConfirmed] bit NOT NULL,
    [TwoFactorEnabled] bit NOT NULL,
    [LockoutEnd] datetimeoffset NULL,
    [LockoutEnabled] bit NOT NULL,
    [AccessFailedCount] int NOT NULL
);

-- Create AspNetRoles table
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(900) NOT NULL PRIMARY KEY,
    [Name] nvarchar(512) NULL,
    [NormalizedName] nvarchar(512) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL
);

-- Create AspNetUserRoles table
CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(900) NOT NULL,
    [RoleId] nvarchar(900) NOT NULL,
    PRIMARY KEY ([UserId], [RoleId]),
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE,
    FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles]([Id]) ON DELETE CASCADE
);

-- Create other ASP.NET Identity tables
CREATE TABLE [AspNetUserClaims] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId] nvarchar(900) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [RoleId] nvarchar(900) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(900) NOT NULL,
    [ProviderKey] nvarchar(900) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(900) NOT NULL,
    PRIMARY KEY ([LoginProvider], [ProviderKey]),
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(900) NOT NULL,
    [LoginProvider] nvarchar(900) NOT NULL,
    [Name] nvarchar(900) NOT NULL,
    [Value] nvarchar(max) NULL,
    PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

-- Create Venues table
CREATE TABLE [Venues] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [LayoutData] nvarchar(max) NOT NULL,
    [Width] int NOT NULL,
    [Height] int NOT NULL,
    [Address] nvarchar(max) NOT NULL,
    [City] nvarchar(max) NOT NULL,
    [HasStaggeredSeating] bit NOT NULL,
    [HasWheelchairSpaces] bit NOT NULL,
    [LayoutType] nvarchar(max) NOT NULL,
    [NumberOfRows] int NOT NULL,
    [RowSpacing] int NOT NULL,
    [SeatSpacing] int NOT NULL,
    [SeatsPerRow] int NOT NULL,
    [WheelchairSpaces] int NOT NULL,
    [AisleWidth] int NOT NULL,
    [HasHorizontalAisles] bit NOT NULL,
    [HasVerticalAisles] bit NOT NULL,
    [HorizontalAisleRows] nvarchar(max) NOT NULL,
    [VerticalAisleSeats] nvarchar(max) NOT NULL,
    [SeatSelectionMode] int NOT NULL
);

-- Create Organizers table
CREATE TABLE [Organizers] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name] nvarchar(max) NOT NULL,
    [ContactEmail] nvarchar(max) NOT NULL,
    [PhoneNumber] nvarchar(max) NOT NULL,
    [UserId] nvarchar(900) NOT NULL,
    [FacebookUrl] nvarchar(max) NULL,
    [YoutubeUrl] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsVerified] bit NOT NULL,
    [OrganizationName] nvarchar(max) NULL,
    [Website] nvarchar(max) NULL,
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id])
);

-- Create Events table
CREATE TABLE [Events] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
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
    FOREIGN KEY ([OrganizerId]) REFERENCES [Organizers]([Id]),
    FOREIGN KEY ([VenueId]) REFERENCES [Venues]([Id])
);

-- Create TicketTypes table
CREATE TABLE [TicketTypes] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Type] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Description] nvarchar(max) NULL,
    [EventId] int NOT NULL,
    [SeatRowAssignments] nvarchar(max) NULL,
    [Color] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NULL,
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create FoodItems table
CREATE TABLE [FoodItems] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [Name] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Description] nvarchar(max) NULL,
    [EventId] int NOT NULL,
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create Tables table
CREATE TABLE [Tables] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [EventId] int NOT NULL,
    [TableNumber] nvarchar(max) NOT NULL,
    [Capacity] int NOT NULL,
    [Height] decimal(18,2) NOT NULL,
    [PricePerSeat] decimal(18,2) NOT NULL,
    [Shape] nvarchar(max) NOT NULL,
    [TablePrice] decimal(18,2) NULL,
    [Width] decimal(18,2) NOT NULL,
    [X] decimal(18,2) NOT NULL,
    [Y] decimal(18,2) NOT NULL,
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create Seats table
CREATE TABLE [Seats] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [EventId] int NOT NULL,
    [Row] nvarchar(max) NOT NULL,
    [Number] int NOT NULL,
    [IsReserved] bit NOT NULL,
    [Height] decimal(18,2) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [ReservedBy] nvarchar(max) NULL,
    [ReservedUntil] datetime2 NULL,
    [SeatNumber] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [TableId] int NULL,
    [Width] decimal(18,2) NOT NULL,
    [X] decimal(18,2) NOT NULL,
    [Y] decimal(18,2) NOT NULL,
    [TicketTypeId] int NOT NULL,
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id]),
    FOREIGN KEY ([TableId]) REFERENCES [Tables]([Id]),
    FOREIGN KEY ([TicketTypeId]) REFERENCES [TicketTypes]([Id])
);

-- Create Bookings table (legacy structure)
CREATE TABLE [Bookings] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [EventId] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaymentIntentId] nvarchar(max) NULL,
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create BookingTickets table (legacy)
CREATE TABLE [BookingTickets] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [BookingId] int NOT NULL,
    [TicketTypeId] int NOT NULL,
    [Quantity] int NOT NULL,
    FOREIGN KEY ([BookingId]) REFERENCES [Bookings]([Id]),
    FOREIGN KEY ([TicketTypeId]) REFERENCES [TicketTypes]([Id])
);

-- Create BookingFoods table (legacy)
CREATE TABLE [BookingFoods] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [BookingId] int NOT NULL,
    [FoodItemId] int NOT NULL,
    [Quantity] int NOT NULL,
    FOREIGN KEY ([BookingId]) REFERENCES [Bookings]([Id]),
    FOREIGN KEY ([FoodItemId]) REFERENCES [FoodItems]([Id])
);

-- Create Payments table
CREATE TABLE [Payments] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
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
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create EventBookings table
CREATE TABLE [EventBookings] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [EventName] nvarchar(max) NOT NULL,
    [SeatNo] nvarchar(max) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [PaymentGUID] nvarchar(max) NOT NULL,
    [BuyerEmail] nvarchar(max) NOT NULL,
    [OrganizerEmail] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [TicketPath] nvarchar(max) NOT NULL,
    [EventID] nvarchar(max) NOT NULL
);

-- Create Reservations table
CREATE TABLE [Reservations] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId] nvarchar(900) NOT NULL,
    [EventId] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    [IsReserved] bit NOT NULL,
    [Number] int NOT NULL,
    [Row] nvarchar(max) NOT NULL,
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]),
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create SeatReservations table
CREATE TABLE [SeatReservations] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [EventId] int NOT NULL,
    [Row] int NOT NULL,
    [Number] int NOT NULL,
    [SessionId] nvarchar(max) NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsConfirmed] bit NOT NULL,
    [UserId] nvarchar(max) NULL,
    FOREIGN KEY ([EventId]) REFERENCES [Events]([Id])
);

-- Create TableReservations table
CREATE TABLE [TableReservations] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [UserId] nvarchar(900) NOT NULL,
    [TableId] int NOT NULL,
    [SeatsReserved] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]),
    FOREIGN KEY ([TableId]) REFERENCES [Tables]([Id])
);

-- Create PaymentRecords table
CREATE TABLE [PaymentRecords] (
    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    [PaymentIntentId] nvarchar(max) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Mobile] nvarchar(max) NULL,
    [EventId] int NOT NULL,
    [EventTitle] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [TicketDetails] nvarchar(max) NOT NULL,
    [FoodDetails] nvarchar(max) NOT NULL,
    [PaymentStatus] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NULL
);

PRINT 'Schema creation completed successfully!';
