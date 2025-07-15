-- Complete Database Setup Script for KiwiLanka Event Booking System
-- This script will ensure the database structure matches the EF models and includes sample data

USE kwdb01;
GO

-- First, let's check and fix the Events table structure
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Name')
BEGIN
    -- If there's a Name column, rename it to Title
    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Title')
    BEGIN
        EXEC sp_rename 'Events.Name', 'Title', 'COLUMN';
        PRINT 'Renamed Events.Name column to Title';
    END
    ELSE
    BEGIN
        -- If both exist, drop the Name column
        ALTER TABLE Events DROP COLUMN Name;
        PRINT 'Dropped duplicate Events.Name column';
    END
END

-- Ensure all required columns exist in Events table
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'Title')
BEGIN
    ALTER TABLE Events ADD Title nvarchar(max) NOT NULL DEFAULT '';
    PRINT 'Added Title column to Events';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'IsActive')
BEGIN
    ALTER TABLE Events ADD IsActive bit NOT NULL DEFAULT 1;
    PRINT 'Added IsActive column to Events';
END

IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Events' AND COLUMN_NAME = 'SeatSelectionMode')
BEGIN
    ALTER TABLE Events ADD SeatSelectionMode int NOT NULL DEFAULT 3;
    PRINT 'Added SeatSelectionMode column to Events';
END

-- Clean up any existing sample data
DELETE FROM BookingFoods;
DELETE FROM BookingTickets;
DELETE FROM Bookings;
DELETE FROM SeatReservations;
DELETE FROM Reservations;
DELETE FROM Seats;
DELETE FROM Tables;
DELETE FROM FoodItems;
DELETE FROM TicketTypes;
DELETE FROM Events;
DELETE FROM Venues;
DELETE FROM Organizers;

-- Reset identity columns
DBCC CHECKIDENT ('Events', RESEED, 0);
DBCC CHECKIDENT ('Venues', RESEED, 0);
DBCC CHECKIDENT ('Organizers', RESEED, 0);
DBCC CHECKIDENT ('TicketTypes', RESEED, 0);
DBCC CHECKIDENT ('FoodItems', RESEED, 0);
DBCC CHECKIDENT ('Seats', RESEED, 0);
DBCC CHECKIDENT ('Tables', RESEED, 0);

PRINT 'Cleaned existing data and reset identity columns';

-- Insert sample Organizers (link with AspNetUsers if they exist)
INSERT INTO Organizers (Name, ContactEmail, PhoneNumber, UserId, CreatedAt, IsVerified, OrganizationName, Website, FacebookUrl, YoutubeUrl)
VALUES 
('John Smith', 'john@kiwievents.co.nz', '+64 21 123 4567', 
 COALESCE((SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'john@kiwievents.co.nz'), NEWID()), 
 GETDATE(), 1, 'KiwiEvents Ltd', 'https://kiwievents.co.nz', 'https://facebook.com/kiwievents', 'https://youtube.com/kiwievents'),

('Sarah Wilson', 'sarah@aucklandentertainment.co.nz', '+64 21 234 5678', 
 COALESCE((SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'sarah@aucklandentertainment.co.nz'), NEWID()), 
 GETDATE(), 1, 'Auckland Entertainment Group', 'https://aucklandentertainment.co.nz', NULL, NULL),

('Mike Johnson', 'mike@wellingtonshows.co.nz', '+64 21 345 6789', 
 COALESCE((SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'mike@wellingtonshows.co.nz'), NEWID()), 
 GETDATE(), 1, 'Wellington Shows', 'https://wellingtonshows.co.nz', NULL, NULL);

PRINT 'Inserted sample Organizers';

-- Insert sample Venues
INSERT INTO Venues (Name, Description, Address, City, Width, Height, LayoutType, NumberOfRows, SeatsPerRow, 
                   RowSpacing, SeatSpacing, HasStaggeredSeating, HasWheelchairSpaces, WheelchairSpaces, 
                   AisleWidth, HasHorizontalAisles, HasVerticalAisles, HorizontalAisleRows, VerticalAisleSeats, 
                   SeatSelectionMode, LayoutData)
VALUES 
('Spark Arena', 'Auckland''s premier indoor entertainment venue', '42-80 Mahuhu Crescent, Auckland CBD', 'Auckland', 
 800, 600, 'Traditional', 50, 40, 80, 60, 1, 1, 20, 120, 1, 1, '25,40', '20,30', 1, 
 '{"rows": 50, "seatsPerRow": 40, "stagePosition": {"x": 400, "y": 50, "width": 200, "height": 50}}'),

('Michael Fowler Centre', 'Wellington''s premier concert hall', '111 Wakefield Street, Wellington', 'Wellington', 
 700, 500, 'Traditional', 40, 35, 75, 55, 1, 1, 15, 110, 1, 1, '20,30', '17,25', 1, 
 '{"rows": 40, "seatsPerRow": 35, "stagePosition": {"x": 350, "y": 40, "width": 180, "height": 45}}'),

('Christchurch Town Hall', 'Historic venue in the heart of Christchurch', '86 Kilmore Street, Christchurch', 'Christchurch', 
 650, 450, 'Traditional', 35, 30, 70, 50, 1, 1, 12, 100, 1, 1, '18,25', '15,22', 1, 
 '{"rows": 35, "seatsPerRow": 30, "stagePosition": {"x": 325, "y": 35, "width": 160, "height": 40}}');

PRINT 'Inserted sample Venues';

-- Insert sample Events
INSERT INTO Events (Title, Description, Date, Location, Price, Capacity, OrganizerId, VenueId, ImageUrl, IsActive, SeatSelectionMode, StagePosition)
VALUES 
('Kiwi Music Festival 2025', 'The biggest music festival in New Zealand featuring international and local artists', 
 DATEADD(DAY, 30, GETDATE()), 'Spark Arena, Auckland', 99.00, 2000, 1, 1, 
 'https://kiwilanka.co.nz/images/music-festival.jpg', 1, 1, 
 '{"x": 400, "y": 50, "width": 200, "height": 50}'),

('Comedy Night Special', 'An evening of laughter with top New Zealand comedians', 
 DATEADD(DAY, 15, GETDATE()), 'Michael Fowler Centre, Wellington', 45.00, 1400, 2, 2, 
 'https://kiwilanka.co.nz/images/comedy-night.jpg', 1, 1, 
 '{"x": 350, "y": 40, "width": 180, "height": 45}'),

('Cultural Dance Performance', 'Traditional Maori and Pacific Island cultural performances', 
 DATEADD(DAY, 45, GETDATE()), 'Christchurch Town Hall, Christchurch', 35.00, 1050, 3, 3, 
 'https://kiwilanka.co.nz/images/cultural-dance.jpg', 1, 1, 
 '{"x": 325, "y": 35, "width": 160, "height": 40}'),

('Business Conference 2025', 'Annual New Zealand Business Leadership Conference', 
 DATEADD(DAY, 60, GETDATE()), 'Spark Arena, Auckland', 150.00, 1500, 1, 1, 
 'https://kiwilanka.co.nz/images/business-conference.jpg', 1, 3, 
 '{"x": 400, "y": 50, "width": 200, "height": 50}'),

('Jazz Night', 'Smooth jazz evening with renowned New Zealand musicians', 
 DATEADD(DAY, 20, GETDATE()), 'Michael Fowler Centre, Wellington', 55.00, 800, 2, 2, 
 'https://kiwilanka.co.nz/images/jazz-night.jpg', 1, 1, 
 '{"x": 350, "y": 40, "width": 180, "height": 45}');

PRINT 'Inserted sample Events';

-- Insert TicketTypes for each event
INSERT INTO TicketTypes (Type, Name, Price, Description, EventId, Color, SeatRowAssignments)
VALUES 
-- Event 1: Kiwi Music Festival
('VIP', 'VIP Premium', 199.00, 'VIP access with front row seating and backstage meet & greet', 1, '#FFD700', '1-5'),
('Premium', 'Premium', 149.00, 'Premium seating with excellent stage view', 1, '#FF6B35', '6-15'),
('Standard', 'Standard', 99.00, 'Standard seating with good stage view', 1, '#007bff', '16-35'),
('General', 'General Admission', 79.00, 'General admission standing area', 1, '#28a745', '36-50'),

-- Event 2: Comedy Night
('Front', 'Front Row', 75.00, 'Front row seating for the best comedy experience', 2, '#FFD700', '1-3'),
('Premium', 'Premium', 55.00, 'Premium seating with excellent view', 2, '#FF6B35', '4-15'),
('Standard', 'Standard', 45.00, 'Standard seating', 2, '#007bff', '16-40'),

-- Event 3: Cultural Dance
('Premium', 'Premium', 50.00, 'Premium seating with excellent view of performances', 3, '#FF6B35', '1-10'),
('Standard', 'Standard', 35.00, 'Standard seating', 3, '#007bff', '11-25'),
('Student', 'Student Discount', 25.00, 'Special student pricing with valid ID', 3, '#28a745', '26-35'),

-- Event 4: Business Conference (General Admission style)
('All Access', 'Conference Pass', 150.00, 'Full conference access including lunch and materials', 4, '#007bff', ''),

-- Event 5: Jazz Night
('VIP', 'VIP Table', 95.00, 'VIP table seating with complimentary drinks', 5, '#FFD700', '1-5'),
('Standard', 'Standard', 55.00, 'Standard seating', 5, '#007bff', '6-40');

PRINT 'Inserted sample TicketTypes';

-- Insert sample FoodItems
INSERT INTO FoodItems (Name, Price, Description, EventId)
VALUES 
-- Food for Music Festival
('Gourmet Burger Combo', 18.50, 'Beef burger with fries and drink', 1),
('Vegetarian Wrap', 14.00, 'Fresh vegetarian wrap with hummus', 1),
('Craft Beer', 8.50, 'Local New Zealand craft beer', 1),
('Soft Drink', 4.50, 'Assorted soft drinks', 1),
('Popcorn', 6.00, 'Fresh buttered popcorn', 1),

-- Food for Comedy Night
('Wine & Cheese Platter', 22.00, 'Selection of NZ wines and cheeses', 2),
('Light Snack Box', 15.00, 'Assorted light snacks and finger foods', 2),
('Coffee', 5.50, 'Premium coffee selection', 2),

-- Food for Cultural Dance
('Traditional Hangi Meal', 25.00, 'Traditional Maori hangi-style meal', 3),
('Pavlova Dessert', 8.00, 'Classic New Zealand pavlova', 3),
('Herbal Tea', 4.00, 'Selection of herbal teas', 3),

-- Food for Business Conference
('Conference Lunch', 28.00, 'Three-course business lunch', 4),
('Morning Tea', 12.00, 'Coffee and pastries', 4),
('Afternoon Tea', 15.00, 'Tea and light refreshments', 4),

-- Food for Jazz Night
('Jazz Dinner Special', 35.00, 'Three-course dinner perfect for jazz night', 5),
('Cocktail Selection', 15.00, 'Classic cocktails to enjoy during the show', 5),
('Dessert & Coffee', 12.00, 'Dessert with premium coffee', 5);

PRINT 'Inserted sample FoodItems';

-- Create some sample seats for seated events (Events 1, 2, 3, 5)
-- Event 1: Music Festival - VIP Section (Rows 1-5)
DECLARE @EventId INT = 1;
DECLARE @Row INT = 1;
DECLARE @SeatNum INT = 1;
DECLARE @TicketTypeId INT;

-- VIP Seats (Rows 1-5, 40 seats per row)
SELECT @TicketTypeId = Id FROM TicketTypes WHERE EventId = @EventId AND Type = 'VIP';
WHILE @Row <= 5
BEGIN
    SET @SeatNum = 1;
    WHILE @SeatNum <= 40
    BEGIN
        INSERT INTO Seats (EventId, Row, Number, SeatNumber, IsReserved, Status, X, Y, Width, Height, Price, TicketTypeId)
        VALUES (@EventId, CHAR(64 + @Row), @SeatNum, CHAR(64 + @Row) + CAST(@SeatNum AS VARCHAR(3)), 
               0, 0, (@SeatNum - 1) * 20, (@Row - 1) * 25, 18, 23, 199.00, @TicketTypeId);
        SET @SeatNum = @SeatNum + 1;
    END
    SET @Row = @Row + 1;
END

-- Premium Seats (Rows 6-15)
SELECT @TicketTypeId = Id FROM TicketTypes WHERE EventId = @EventId AND Type = 'Premium';
WHILE @Row <= 15
BEGIN
    SET @SeatNum = 1;
    WHILE @SeatNum <= 40
    BEGIN
        INSERT INTO Seats (EventId, Row, Number, SeatNumber, IsReserved, Status, X, Y, Width, Height, Price, TicketTypeId)
        VALUES (@EventId, CHAR(64 + @Row), @SeatNum, CHAR(64 + @Row) + CAST(@SeatNum AS VARCHAR(3)), 
               0, 0, (@SeatNum - 1) * 20, (@Row - 1) * 25, 18, 23, 149.00, @TicketTypeId);
        SET @SeatNum = @SeatNum + 1;
    END
    SET @Row = @Row + 1;
END

PRINT 'Created sample seats for Event 1 (Music Festival)';

-- Event 2: Comedy Night - Create seats for rows 1-20 (smaller venue)
SET @EventId = 2;
SET @Row = 1;

-- Front Row seats
SELECT @TicketTypeId = Id FROM TicketTypes WHERE EventId = @EventId AND Type = 'Front';
WHILE @Row <= 3
BEGIN
    SET @SeatNum = 1;
    WHILE @SeatNum <= 35
    BEGIN
        INSERT INTO Seats (EventId, Row, Number, SeatNumber, IsReserved, Status, X, Y, Width, Height, Price, TicketTypeId)
        VALUES (@EventId, CHAR(64 + @Row), @SeatNum, CHAR(64 + @Row) + CAST(@SeatNum AS VARCHAR(3)), 
               0, 0, (@SeatNum - 1) * 20, (@Row - 1) * 25, 18, 23, 75.00, @TicketTypeId);
        SET @SeatNum = @SeatNum + 1;
    END
    SET @Row = @Row + 1;
END

-- Premium seats (Rows 4-15)
SELECT @TicketTypeId = Id FROM TicketTypes WHERE EventId = @EventId AND Type = 'Premium';
WHILE @Row <= 15
BEGIN
    SET @SeatNum = 1;
    WHILE @SeatNum <= 35
    BEGIN
        INSERT INTO Seats (EventId, Row, Number, SeatNumber, IsReserved, Status, X, Y, Width, Height, Price, TicketTypeId)
        VALUES (@EventId, CHAR(64 + @Row), @SeatNum, CHAR(64 + @Row) + CAST(@SeatNum AS VARCHAR(3)), 
               0, 0, (@SeatNum - 1) * 20, (@Row - 1) * 25, 18, 23, 55.00, @TicketTypeId);
        SET @SeatNum = @SeatNum + 1;
    END
    SET @Row = @Row + 1;
END

PRINT 'Created sample seats for Event 2 (Comedy Night)';

-- Verify data was inserted
SELECT 'Events' as TableName, COUNT(*) as RecordCount FROM Events
UNION ALL
SELECT 'Venues', COUNT(*) FROM Venues
UNION ALL
SELECT 'Organizers', COUNT(*) FROM Organizers
UNION ALL
SELECT 'TicketTypes', COUNT(*) FROM TicketTypes
UNION ALL
SELECT 'FoodItems', COUNT(*) FROM FoodItems
UNION ALL
SELECT 'Seats', COUNT(*) FROM Seats;

PRINT 'Database setup completed successfully!';
PRINT 'Sample data includes:';
PRINT '- 3 Organizers';
PRINT '- 3 Venues (Auckland, Wellington, Christchurch)';
PRINT '- 5 Events (various types and dates)';
PRINT '- Multiple TicketTypes per event';
PRINT '- Food items for each event';
PRINT '- Seats for seated events';
PRINT '';
PRINT 'You can now test the API endpoints:';
PRINT '- GET /Events - Should return 5 events';
PRINT '- GET /Events/1 - Should return Music Festival details';
PRINT '- GET /Venues - Should return 3 venues';
PRINT '- GET /Organizers - Should return 3 organizers';
