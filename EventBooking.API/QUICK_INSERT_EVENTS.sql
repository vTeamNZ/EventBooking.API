-- Quick Insert for Events
SET IDENTITY_INSERT Events ON;

INSERT INTO Events (Id, Title, Description, Date, Location, Price, Capacity, OrganizerId, ImageUrl, IsActive, SeatSelectionMode, StagePosition, VenueId) VALUES 
(4, 'Ladies Night', 'Canta Lankans Women''s Club hosts a fun DJ night for ladies.', '2025-08-15 18:00:00', '341 Mohoao Auditorium, Halswell Centre, Christchurch, New Zealand', 20.00, 150, 4, '/events/2.jpg', 1, 3, '', NULL),
(6, 'Sanketha', 'Ruchi Events with Sanketha', '2025-10-25 19:00:00', '145 Godley Road, Auckland, New Zealand', 0.00, 416, 6, '/events/sanketha.jpg', 1, 1, '', NULL),
(19, 'TLS Music Night 8th Aug', 'Get ready for another unforgettable night of beats, vibes, and pure entertainment', '2025-08-08 19:00:00', '172/174 Remuera Road, Remuera, Auckland 1050, New Zealand', 0.00, 300, 6, '/events/tls2.jpg', 1, 1, '', NULL),
(21, 'TestEvent10', 'Test description: We will dissect 7 distinct event description examples, covering various event types and industries.', '2025-08-30 19:00:00', 'Test Location', 0.00, 100, 6, '/events/default.jpg', 1, 1, '', NULL);

SET IDENTITY_INSERT Events OFF;
