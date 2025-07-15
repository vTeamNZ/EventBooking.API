-- Simple data population script
USE kwdb01;
GO

-- Clean existing data (simple approach)
DELETE FROM Seats WHERE EventId IS NOT NULL;
DELETE FROM FoodItems WHERE EventId IS NOT NULL;
DELETE FROM TicketTypes WHERE EventId IS NOT NULL;
DELETE FROM Events WHERE Id IS NOT NULL;
DELETE FROM Organizers WHERE Id IS NOT NULL;

-- Reset identity
DBCC CHECKIDENT ('Events', RESEED, 0);
DBCC CHECKIDENT ('Organizers', RESEED, 0);
DBCC CHECKIDENT ('TicketTypes', RESEED, 0);
DBCC CHECKIDENT ('FoodItems', RESEED, 0);
DBCC CHECKIDENT ('Seats', RESEED, 0);

-- Insert sample Organizers (without foreign key issues)
INSERT INTO Organizers (Name, ContactEmail, PhoneNumber, CreatedAt, IsVerified, OrganizationName, Website)
VALUES 
('John Smith', 'john@kiwievents.co.nz', '+64 21 123 4567', GETDATE(), 1, 'KiwiEvents Ltd', 'https://kiwievents.co.nz'),
('Sarah Wilson', 'sarah@aucklandentertainment.co.nz', '+64 21 234 5678', GETDATE(), 1, 'Auckland Entertainment Group', 'https://aucklandentertainment.co.nz'),
('Mike Johnson', 'mike@wellingtonshows.co.nz', '+64 21 345 6789', GETDATE(), 1, 'Wellington Shows', 'https://wellingtonshows.co.nz');

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

-- Insert TicketTypes (using the Name column we added)
INSERT INTO TicketTypes (Type, Name, Price, Description, EventId, Color, SeatRowAssignments)
VALUES 
('VIP', 'VIP Premium', 199.00, 'VIP access with front row seating', 1, '#FFD700', '1-5'),
('Premium', 'Premium', 149.00, 'Premium seating with excellent view', 1, '#FF6B35', '6-15'),
('Standard', 'Standard', 99.00, 'Standard seating', 1, '#007bff', '16-35'),
('Front', 'Front Row', 75.00, 'Front row seating', 2, '#FFD700', '1-3'),
('Premium', 'Premium', 55.00, 'Premium seating', 2, '#FF6B35', '4-15'),
('Premium', 'Premium', 50.00, 'Premium seating', 3, '#FF6B35', '1-10'),
('Standard', 'Standard', 35.00, 'Standard seating', 3, '#007bff', '11-25'),
('All Access', 'Conference Pass', 150.00, 'Full conference access', 4, '#007bff', ''),
('VIP', 'VIP Table', 95.00, 'VIP table seating', 5, '#FFD700', '1-5'),
('Standard', 'Standard', 55.00, 'Standard seating', 5, '#007bff', '6-40');

-- Insert some sample food items
INSERT INTO FoodItems (Name, Price, Description, EventId)
VALUES 
('Gourmet Burger Combo', 18.50, 'Beef burger with fries and drink', 1),
('Craft Beer', 8.50, 'Local New Zealand craft beer', 1),
('Wine & Cheese Platter', 22.00, 'Selection of NZ wines and cheeses', 2),
('Traditional Hangi Meal', 25.00, 'Traditional Maori hangi-style meal', 3),
('Conference Lunch', 28.00, 'Three-course business lunch', 4),
('Jazz Dinner Special', 35.00, 'Three-course dinner', 5);

-- Create a few sample seats using TicketTypeId
INSERT INTO Seats (EventId, Row, Number, SeatNumber, IsReserved, Status, X, Y, Width, Height, Price, TicketTypeId)
VALUES 
-- VIP seats for Event 1
(1, 'A', 1, 'A1', 0, 0, 0, 0, 18, 23, 199.00, 1),
(1, 'A', 2, 'A2', 0, 0, 20, 0, 18, 23, 199.00, 1),
(1, 'A', 3, 'A3', 0, 0, 40, 0, 18, 23, 199.00, 1),
(1, 'B', 1, 'B1', 0, 0, 0, 25, 18, 23, 199.00, 1),
(1, 'B', 2, 'B2', 0, 0, 20, 25, 18, 23, 199.00, 1),
-- Premium seats for Event 1
(1, 'F', 1, 'F1', 0, 0, 0, 125, 18, 23, 149.00, 2),
(1, 'F', 2, 'F2', 0, 0, 20, 125, 18, 23, 149.00, 2),
(1, 'F', 3, 'F3', 0, 0, 40, 125, 18, 23, 149.00, 2),
-- Front row seats for Event 2
(2, 'A', 1, 'A1', 0, 0, 0, 0, 18, 23, 75.00, 4),
(2, 'A', 2, 'A2', 0, 0, 20, 0, 18, 23, 75.00, 4);

-- Show results
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

PRINT 'Database populated successfully!';
PRINT 'Test your API: https://kiwilanka.co.nz/api/Events';
