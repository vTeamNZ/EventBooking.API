-- Very simple test - just add a minimal event to test the API
USE kwdb01;
GO

-- First, let's see what users exist
SELECT COUNT(*) as UserCount FROM AspNetUsers;

-- If there are users, use one, otherwise we'll need to handle this differently
DECLARE @UserId NVARCHAR(450);
SELECT TOP 1 @UserId = Id FROM AspNetUsers;

IF @UserId IS NOT NULL
BEGIN
    PRINT 'Using existing user ID: ' + @UserId;
    
    -- Insert one organizer
    INSERT INTO Organizers (Name, ContactEmail, PhoneNumber, UserId, CreatedAt, IsVerified, OrganizationName)
    VALUES ('Test Organizer', 'test@kiwilanka.co.nz', '+64 21 123 4567', @UserId, GETDATE(), 1, 'Test Organization');
    
    -- Insert one event
    INSERT INTO Events (Title, Description, Date, Location, Price, Capacity, OrganizerId, VenueId, ImageUrl, IsActive, SeatSelectionMode)
    VALUES ('Test Event', 'A test event to verify the API works', DATEADD(DAY, 30, GETDATE()), 'Test Location', 50.00, 100, 1, 1, 'https://example.com/test.jpg', 1, 3);
    
    -- Insert one ticket type
    INSERT INTO TicketTypes (Type, Name, Price, Description, EventId, Color)
    VALUES ('Standard', 'Standard Ticket', 50.00, 'Standard admission', 1, '#007bff');
    
    PRINT 'Test data inserted successfully!';
    
    -- Verify
    SELECT 'Events' as TableName, COUNT(*) as RecordCount FROM Events
    UNION ALL
    SELECT 'Organizers', COUNT(*) FROM Organizers
    UNION ALL
    SELECT 'TicketTypes', COUNT(*) FROM TicketTypes;
END
ELSE
BEGIN
    PRINT 'No users found. Need to create a user first or modify the Organizers table structure.';
    
    -- Let's check if we can make UserId nullable temporarily
    SELECT COLUMN_NAME, IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Organizers' AND COLUMN_NAME = 'UserId';
END
