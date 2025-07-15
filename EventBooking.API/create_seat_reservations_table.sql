-- Check if the table already exists
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'SeatReservations')
BEGIN
    -- Create the SeatReservations table
    CREATE TABLE [dbo].[SeatReservations](
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [EventId] [int] NOT NULL,
        [Row] [int] NOT NULL,
        [Number] [int] NOT NULL,
        [SessionId] [nvarchar](max) NOT NULL,
        [ReservedAt] [datetime2](7) NOT NULL,
        [ExpiresAt] [datetime2](7) NOT NULL,
        [IsConfirmed] [bit] NOT NULL,
        [UserId] [nvarchar](max) NULL,
        CONSTRAINT [PK_SeatReservations] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    -- Create indexes
    CREATE INDEX [IX_SeatReservations_EventId_IsConfirmed_ExpiresAt] ON [dbo].[SeatReservations]
    (
        [EventId] ASC,
        [IsConfirmed] ASC,
        [ExpiresAt] ASC
    );

    CREATE INDEX [IX_SeatReservations_EventId_Row_Number] ON [dbo].[SeatReservations]
    (
        [EventId] ASC,
        [Row] ASC,
        [Number] ASC
    );

    -- Add foreign key
    ALTER TABLE [dbo].[SeatReservations] WITH CHECK ADD CONSTRAINT [FK_SeatReservations_Events_EventId]
        FOREIGN KEY([EventId]) REFERENCES [dbo].[Events] ([Id]) ON DELETE CASCADE;

    PRINT 'SeatReservations table created successfully.';
END
ELSE
BEGIN
    PRINT 'SeatReservations table already exists.';
END
