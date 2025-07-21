-- Step 2: Create remaining business tables
-- Based on the database structure provided

PRINT 'Creating Organizers table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Organizers' AND xtype='U')
CREATE TABLE [Organizers] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [ContactEmail] nvarchar(max) NOT NULL,
    [PhoneNumber] nvarchar(max) NOT NULL,
    [UserId] nvarchar(450) NOT NULL,
    [FacebookUrl] nvarchar(max) NULL,
    [YoutubeUrl] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [IsVerified] bit NOT NULL,
    [OrganizationName] nvarchar(max) NULL,
    [Website] nvarchar(max) NULL,
    CONSTRAINT [PK_Organizers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Organizers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

PRINT 'Creating Venues table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Venues' AND xtype='U')
CREATE TABLE [Venues] (
    [Id] int IDENTITY(1,1) NOT NULL,
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
    [SeatSelectionMode] int NOT NULL,
    CONSTRAINT [PK_Venues] PRIMARY KEY ([Id])
);

PRINT 'Creating FoodItems table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='FoodItems' AND xtype='U')
CREATE TABLE [FoodItems] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Description] nvarchar(max) NULL,
    [EventId] int NOT NULL,
    CONSTRAINT [PK_FoodItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_FoodItems_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

PRINT 'Creating BookingFoods table...'
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='BookingFoods' AND xtype='U')
CREATE TABLE [BookingFoods] (
    [Id] int IDENTITY(1,1) NOT NULL,
    [BookingId] int NOT NULL,
    [FoodItemId] int NOT NULL,
    [Quantity] int NOT NULL,
    CONSTRAINT [PK_BookingFoods] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_BookingFoods_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_BookingFoods_FoodItems_FoodItemId] FOREIGN KEY ([FoodItemId]) REFERENCES [FoodItems] ([Id]) ON DELETE NO ACTION
);

PRINT 'Business tables created successfully!'
