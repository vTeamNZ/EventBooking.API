-- 1. Add required columns to TicketTypes if they don't exist
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
BEGIN
    ALTER TABLE TicketTypes ADD Name nvarchar(100) NOT NULL DEFAULT ''
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
BEGIN
    ALTER TABLE TicketTypes ADD Color nvarchar(7) NOT NULL DEFAULT '#007bff'
END

-- 2. Ensure TicketTypeId exists in Seats table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    ALTER TABLE Seats ADD TicketTypeId int NULL
END

-- Make sure TicketTypes have these columns
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Description')
BEGIN
    ALTER TABLE TicketTypes ADD Description nvarchar(max) NULL
END

-- 3. Copy Section data to TicketTypes and migrate relationships
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Sections')
BEGIN
    -- First ensure columns exist before using them
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Name')
    BEGIN
        ALTER TABLE TicketTypes ADD Name nvarchar(100) NOT NULL DEFAULT ''
    END

    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'TicketTypes' AND COLUMN_NAME = 'Color')
    BEGIN
        ALTER TABLE TicketTypes ADD Color nvarchar(7) NOT NULL DEFAULT '#007bff'
    END

    -- Now populate Name from Type
    UPDATE TicketTypes 
    SET Name = Type 
    WHERE Name = '' OR Name IS NULL;

    -- Update existing TicketTypes with Section data where there's a match
    UPDATE tt
    SET tt.Name = CASE 
            WHEN s.Name IS NOT NULL AND s.Name != '' THEN s.Name 
            ELSE tt.Type 
        END,
        tt.Color = COALESCE(s.Color, tt.Color),
        tt.Price = COALESCE(s.BasePrice, tt.Price)
    FROM TicketTypes tt
    LEFT JOIN Sections s ON tt.Type = s.Name;

    -- Create new TicketTypes for Sections that don't have corresponding TicketTypes
    INSERT INTO TicketTypes (Type, Name, Color, Price, EventId, Description)
    SELECT DISTINCT 
        s.Name,
        s.Name,
        COALESCE(s.Color, '#007bff'),
        COALESCE(s.BasePrice, 0),
        e.Id as EventId,
        'Migrated from Section: ' + s.Name
    FROM Sections s
    CROSS JOIN Events e
    WHERE NOT EXISTS (
        SELECT 1 FROM TicketTypes tt 
        WHERE tt.EventId = e.Id AND (tt.Name = s.Name OR tt.Type = s.Name)
    )
    AND EXISTS (
        SELECT 1 FROM Seats seat 
        WHERE seat.SectionId = s.Id AND seat.EventId = e.Id
    );

    -- Update seats to reference TicketTypes
    UPDATE seat
    SET seat.TicketTypeId = tt.Id
    FROM Seats seat
    INNER JOIN Sections s ON seat.SectionId = s.Id
    INNER JOIN TicketTypes tt ON (tt.Name = s.Name OR tt.Type = s.Name) AND tt.EventId = seat.EventId
    WHERE seat.SectionId IS NOT NULL AND seat.TicketTypeId IS NULL;

    -- Update tables to reference TicketTypes if Tables table exists
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Tables')
    BEGIN
        IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                   WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
        BEGIN
            -- Add TicketTypeId to Tables if it doesn't exist
            IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
                           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'TicketTypeId')
            BEGIN
                ALTER TABLE Tables ADD TicketTypeId int NULL
            END

            -- Update tables to reference TicketTypes
            UPDATE t
            SET t.TicketTypeId = tt.Id
            FROM Tables t
            INNER JOIN Sections s ON t.SectionId = s.Id
            INNER JOIN TicketTypes tt ON (tt.Name = s.Name OR tt.Type = s.Name) AND tt.EventId = t.EventId
            WHERE t.SectionId IS NOT NULL AND t.TicketTypeId IS NULL;
        END
    END

    -- Drop foreign key constraints
    DECLARE @sql NVARCHAR(MAX) = '';
    SELECT @sql = @sql + 'ALTER TABLE ' + QUOTENAME(fk.TABLE_SCHEMA) + '.' + QUOTENAME(fk.TABLE_NAME) + ' DROP CONSTRAINT ' + QUOTENAME(fk.CONSTRAINT_NAME) + ';' + CHAR(13)
    FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS fk
    WHERE fk.REFERENCED_TABLE_NAME = 'Sections';
    
    IF @sql != ''
        EXEC sp_executesql @sql;

    -- Drop SectionId columns
    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'SectionId')
    BEGIN
        ALTER TABLE Seats DROP COLUMN SectionId
    END

    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'SectionId')
    BEGIN
        ALTER TABLE Tables DROP COLUMN SectionId
    END

    -- Drop Sections table
    DROP TABLE Sections;
END

-- 4. Update any seats without TicketTypeId to reference first available TicketType
UPDATE s 
SET s.TicketTypeId = (
    SELECT TOP 1 tt.Id 
    FROM TicketTypes tt 
    WHERE tt.EventId = s.EventId
    ORDER BY tt.Id
)
FROM Seats s
WHERE s.TicketTypeId IS NULL
AND EXISTS (SELECT 1 FROM TicketTypes tt WHERE tt.EventId = s.EventId);

-- 5. Make TicketTypeId NOT NULL after data migration
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Seats' AND COLUMN_NAME = 'TicketTypeId' AND IS_NULLABLE = 'YES')
BEGIN
    ALTER TABLE Seats ALTER COLUMN TicketTypeId int NOT NULL
END

-- 6. Create foreign key constraint for Seats -> TicketTypes
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
               WHERE CONSTRAINT_NAME = 'FK_Seats_TicketTypes_TicketTypeId')
BEGIN
    ALTER TABLE Seats 
    ADD CONSTRAINT FK_Seats_TicketTypes_TicketTypeId 
    FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id)
END

-- 7. Create foreign key constraint for Tables -> TicketTypes if Tables table exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Tables')
AND EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
                   WHERE CONSTRAINT_NAME = 'FK_Tables_TicketTypes_TicketTypeId')
    BEGIN
        -- Make TicketTypeId NOT NULL for Tables if it has data
        UPDATE t 
        SET t.TicketTypeId = (
            SELECT TOP 1 tt.Id 
            FROM TicketTypes tt 
            WHERE tt.EventId = t.EventId
            ORDER BY tt.Id
        )
        FROM Tables t
        WHERE t.TicketTypeId IS NULL
        AND EXISTS (SELECT 1 FROM TicketTypes tt WHERE tt.EventId = t.EventId);

        ALTER TABLE Tables ALTER COLUMN TicketTypeId int NOT NULL;
        
        ALTER TABLE Tables 
        ADD CONSTRAINT FK_Tables_TicketTypes_TicketTypeId 
        FOREIGN KEY (TicketTypeId) REFERENCES TicketTypes(Id)
    END
END

-- 8. Create indexes for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Seats_TicketTypeId')
BEGIN
    CREATE INDEX IX_Seats_TicketTypeId ON Seats(TicketTypeId)
END

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Tables')
AND EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
           WHERE TABLE_NAME = 'Tables' AND COLUMN_NAME = 'TicketTypeId')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Tables_TicketTypeId')
    BEGIN
        CREATE INDEX IX_Tables_TicketTypeId ON Tables(TicketTypeId)
    END
END

-- 9. Ensure all TicketTypes have proper names (fallback to Type)
UPDATE TicketTypes 
SET Name = Type 
WHERE Name = '' OR Name IS NULL;

BEGIN TRANSACTION;
CREATE TABLE [AspNetRoles] (
    [Id] nvarchar(450) NOT NULL,
    [Name] nvarchar(256) NULL,
    [NormalizedName] nvarchar(256) NULL,
    [ConcurrencyStamp] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetUsers] (
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

CREATE TABLE [Organizers] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(max) NOT NULL,
    [ContactEmail] nvarchar(max) NOT NULL,
    [PhoneNumber] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Organizers] PRIMARY KEY ([Id])
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [FullName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE TABLE [AspNetRoleClaims] (
    [Id] int NOT NULL IDENTITY,
    [RoleId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetRoleClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserClaims] (
    [Id] int NOT NULL IDENTITY,
    [UserId] nvarchar(450) NOT NULL,
    [ClaimType] nvarchar(max) NULL,
    [ClaimValue] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserClaims] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserLogins] (
    [LoginProvider] nvarchar(450) NOT NULL,
    [ProviderKey] nvarchar(450) NOT NULL,
    [ProviderDisplayName] nvarchar(max) NULL,
    [UserId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserRoles] (
    [UserId] nvarchar(450) NOT NULL,
    [RoleId] nvarchar(450) NOT NULL,
    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [AspNetRoles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AspNetUserTokens] (
    [UserId] nvarchar(450) NOT NULL,
    [LoginProvider] nvarchar(450) NOT NULL,
    [Name] nvarchar(450) NOT NULL,
    [Value] nvarchar(max) NULL,
    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Events] (
    [Id] int NOT NULL IDENTITY,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Date] datetime2 NOT NULL,
    [Location] nvarchar(max) NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [Capacity] int NOT NULL,
    [OrganizerId] int NOT NULL,
    CONSTRAINT [PK_Events] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Events_Organizers_OrganizerId] FOREIGN KEY ([OrganizerId]) REFERENCES [Organizers] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Seats] (
    [Id] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [Row] nvarchar(max) NOT NULL,
    [Number] int NOT NULL,
    [IsReserved] bit NOT NULL,
    CONSTRAINT [PK_Seats] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Seats_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Tables] (
    [Id] int NOT NULL IDENTITY,
    [EventId] int NOT NULL,
    [TableNumber] nvarchar(max) NOT NULL,
    [Capacity] int NOT NULL,
    CONSTRAINT [PK_Tables] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tables_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Reservations] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [EventId] int NOT NULL,
    [Quantity] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    [SeatId] int NULL,
    CONSTRAINT [PK_Reservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Reservations_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Reservations_Seats_SeatId] FOREIGN KEY ([SeatId]) REFERENCES [Seats] ([Id]),
    CONSTRAINT [FK_Reservations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [TableReservations] (
    [Id] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [TableId] int NOT NULL,
    [SeatsReserved] int NOT NULL,
    [ReservedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_TableReservations] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_TableReservations_Tables_TableId] FOREIGN KEY ([TableId]) REFERENCES [Tables] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_TableReservations_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [AspNetRoleClaims] ([RoleId]);

CREATE UNIQUE INDEX [RoleNameIndex] ON [AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

CREATE INDEX [IX_AspNetUserClaims_UserId] ON [AspNetUserClaims] ([UserId]);

CREATE INDEX [IX_AspNetUserLogins_UserId] ON [AspNetUserLogins] ([UserId]);

CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [AspNetUserRoles] ([RoleId]);

CREATE INDEX [EmailIndex] ON [AspNetUsers] ([NormalizedEmail]);

CREATE UNIQUE INDEX [UserNameIndex] ON [AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

CREATE INDEX [IX_Events_OrganizerId] ON [Events] ([OrganizerId]);

CREATE INDEX [IX_Reservations_EventId] ON [Reservations] ([EventId]);

CREATE INDEX [IX_Reservations_SeatId] ON [Reservations] ([SeatId]);

CREATE INDEX [IX_Reservations_UserId] ON [Reservations] ([UserId]);

CREATE INDEX [IX_Seats_EventId] ON [Seats] ([EventId]);

CREATE INDEX [IX_TableReservations_TableId] ON [TableReservations] ([TableId]);

CREATE INDEX [IX_TableReservations_UserId] ON [TableReservations] ([UserId]);

CREATE INDEX [IX_Tables_EventId] ON [Tables] ([EventId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250601133545_InitialCreate', N'9.0.5');

ALTER TABLE [Organizers] ADD [UserId] nvarchar(450) NOT NULL DEFAULT N'';

CREATE UNIQUE INDEX [IX_Organizers_UserId] ON [Organizers] ([UserId]);

ALTER TABLE [Organizers] ADD CONSTRAINT [FK_Organizers_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250601222452_LinkOrganizerToUser', N'9.0.5');

ALTER TABLE [Reservations] DROP CONSTRAINT [FK_Reservations_Users_UserId];

DROP INDEX [IX_Reservations_UserId] ON [Reservations];
DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservations]') AND [c].[name] = N'UserId');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Reservations] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [Reservations] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;
CREATE INDEX [IX_Reservations_UserId] ON [Reservations] ([UserId]);

ALTER TABLE [Reservations] ADD [IsReserved] bit NOT NULL DEFAULT CAST(0 AS bit);

ALTER TABLE [Reservations] ADD [Number] int NOT NULL DEFAULT 0;

ALTER TABLE [Reservations] ADD [Row] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [Reservations] ADD [UserId1] int NULL;

CREATE INDEX [IX_Reservations_UserId1] ON [Reservations] ([UserId1]);

ALTER TABLE [Reservations] ADD CONSTRAINT [FK_Reservations_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION;

ALTER TABLE [Reservations] ADD CONSTRAINT [FK_Reservations_Users_UserId1] FOREIGN KEY ([UserId1]) REFERENCES [Users] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250602012309_FixCascadeDelete', N'9.0.5');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250602013348_FixReservationUserFk', N'9.0.5');

ALTER TABLE [Reservations] DROP CONSTRAINT [FK_Reservations_Seats_SeatId];

DROP INDEX [IX_Reservations_SeatId] ON [Reservations];

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservations]') AND [c].[name] = N'Quantity');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Reservations] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Reservations] DROP COLUMN [Quantity];

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservations]') AND [c].[name] = N'SeatId');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Reservations] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Reservations] DROP COLUMN [SeatId];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250602014045_FixReservationModelAgain', N'9.0.5');

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250602014321_FinalReservationFix', N'9.0.5');

ALTER TABLE [Events] DROP CONSTRAINT [FK_Events_Organizers_OrganizerId];

DECLARE @var3 sysname;
SELECT @var3 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Events]') AND [c].[name] = N'Price');
IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Events] DROP CONSTRAINT [' + @var3 + '];');
ALTER TABLE [Events] ALTER COLUMN [Price] decimal(18,2) NULL;

DECLARE @var4 sysname;
SELECT @var4 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Events]') AND [c].[name] = N'OrganizerId');
IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Events] DROP CONSTRAINT [' + @var4 + '];');
ALTER TABLE [Events] ALTER COLUMN [OrganizerId] int NULL;

DECLARE @var5 sysname;
SELECT @var5 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Events]') AND [c].[name] = N'Date');
IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Events] DROP CONSTRAINT [' + @var5 + '];');
ALTER TABLE [Events] ALTER COLUMN [Date] datetime2 NULL;

DECLARE @var6 sysname;
SELECT @var6 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Events]') AND [c].[name] = N'Capacity');
IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Events] DROP CONSTRAINT [' + @var6 + '];');
ALTER TABLE [Events] ALTER COLUMN [Capacity] int NULL;

ALTER TABLE [Events] ADD [ImageUrl] nvarchar(max) NULL;

ALTER TABLE [Events] ADD CONSTRAINT [FK_Events_Organizers_OrganizerId] FOREIGN KEY ([OrganizerId]) REFERENCES [Organizers] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250607011807_AddImageUrlToEvent', N'9.0.5');

ALTER TABLE [Reservations] DROP CONSTRAINT [FK_Reservations_Users_UserId1];

ALTER TABLE [TableReservations] DROP CONSTRAINT [FK_TableReservations_Users_UserId];

ALTER TABLE [Users] DROP CONSTRAINT [PK_Users];

EXEC sp_rename N'[Users]', N'User', 'OBJECT';

ALTER TABLE [User] ADD CONSTRAINT [PK_User] PRIMARY KEY ([Id]);

CREATE TABLE [Payments] (
    [Id] int NOT NULL IDENTITY,
    [PaymentIntentId] nvarchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(5) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [EventId] int NOT NULL,
    [TicketDetails] nvarchar(max) NOT NULL,
    [FoodDetails] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_Events_EventId] FOREIGN KEY ([EventId]) REFERENCES [Events] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Payments_EventId] ON [Payments] ([EventId]);

ALTER TABLE [Reservations] ADD CONSTRAINT [FK_Reservations_User_UserId1] FOREIGN KEY ([UserId1]) REFERENCES [User] ([Id]);

ALTER TABLE [TableReservations] ADD CONSTRAINT [FK_TableReservations_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]) ON DELETE CASCADE;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250613002636_AddPaymentRelations', N'9.0.5');

CREATE TABLE [PaymentRecords] (
    [Id] int NOT NULL IDENTITY,
    [PaymentIntentId] nvarchar(max) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Mobile] nvarchar(max) NOT NULL,
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

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250613054333_AddPaymentRecordTable', N'9.0.5');

DECLARE @var7 sysname;
SELECT @var7 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'CreatedAt');
IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT [' + @var7 + '];');
ALTER TABLE [Payments] DROP COLUMN [CreatedAt];

DECLARE @var8 sysname;
SELECT @var8 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'UpdatedAt');
IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT [' + @var8 + '];');
ALTER TABLE [Payments] ALTER COLUMN [UpdatedAt] datetime2 NULL;

DECLARE @var9 sysname;
SELECT @var9 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'PaymentIntentId');
IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT [' + @var9 + '];');
ALTER TABLE [Payments] ALTER COLUMN [PaymentIntentId] nvarchar(255) NOT NULL;

DECLARE @var10 sysname;
SELECT @var10 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payments]') AND [c].[name] = N'Currency');
IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Payments] DROP CONSTRAINT [' + @var10 + '];');
ALTER TABLE [Payments] ALTER COLUMN [Currency] nvarchar(10) NOT NULL;

ALTER TABLE [Payments] ADD [Description] nvarchar(500) NOT NULL DEFAULT N'';

ALTER TABLE [Payments] ADD [Email] nvarchar(255) NOT NULL DEFAULT N'';

ALTER TABLE [Payments] ADD [FirstName] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [Payments] ADD [LastName] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [Payments] ADD [Mobile] nvarchar(50) NOT NULL DEFAULT N'';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20250613090744_AddCustomerDetailsToPayments', N'9.0.5');

COMMIT;
GO

