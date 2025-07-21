-- Step 3: Create remaining complex tables
-- Based on the database structure provided

PRINT 'Creating Reservations table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Reservations' AND xtype='U')
CREATE TABLE [Reservations] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [EventId] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    [IsReserved] bit NOT NULL,
    [Number] int NOT NULL,
    [Row] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Reservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reservations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Reservations_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

PRINT 'Creating Tables table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Tables' AND xtype='U')
CREATE TABLE [Tables] (
    [Id] int IDENTITY(1,1) NOT NULL,
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
    CONSTRAINT [PK_Tables] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tables_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

PRINT 'Creating Seats table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Seats' AND xtype='U')
CREATE TABLE [Seats] (
    [Id] int IDENTITY(1,1) NOT NULL,
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
    CONSTRAINT [PK_Seats] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Seats_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Seats_Tables_TableId] FOREIGN KEY ([TableId]) REFERENCES [Tables] ([Id]),
    CONSTRAINT [FK_Seats_TicketTypes_TicketTypeId] FOREIGN KEY ([TicketTypeId]) REFERENCES [TicketTypes] ([Id]) ON DELETE NO ACTION
);

PRINT 'Creating SeatReservations table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SeatReservations' AND xtype='U')
CREATE TABLE [SeatReservations] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [EventId] int NOT NULL,
    [Row] int NOT NULL,
    [Number] int NOT NULL,
    [SessionId] nvarchar(max) NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [IsConfirmed] bit NOT NULL,
    [UserId] nvarchar(max) NULL,
    CONSTRAINT [PK_SeatReservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SeatReservations_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

PRINT 'Creating TableReservations table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TableReservations' AND xtype='U')
CREATE TABLE [TableReservations] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [TableId] int NOT NULL,
    [SeatsReserved] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_TableReservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TableReservations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TableReservations_Tables_TableId] FOREIGN KEY ([TableId]) REFERENCES [Tables] ([Id]) ON DELETE NO ACTION
);

PRINT 'Creating PaymentRecords table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PaymentRecords' AND xtype='U')
CREATE TABLE [PaymentRecords] (
    [Id] int IDENTITY(1,1) NOT NULL,
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
    [UpdatedAt] datetime2 NULL,
    CONSTRAINT [PK_PaymentRecords] PRIMARY KEY ([Id])
);

PRINT 'Creating EventBookings table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EventBookings' AND xtype='U')
CREATE TABLE [EventBookings] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [EventName] nvarchar(max) NOT NULL,
    [SeatNo] nvarchar(max) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [PaymentGUID] nvarchar(max) NOT NULL,
    [BuyerEmail] nvarchar(max) NOT NULL,
    [OrganizerEmail] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [TicketPath] nvarchar(max) NOT NULL,
    [EventID] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_EventBookings] PRIMARY KEY ([Id])
);

PRINT 'Complex tables created successfully!'
