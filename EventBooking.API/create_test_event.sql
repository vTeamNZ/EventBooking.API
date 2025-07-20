-- Insert test event for payment testing
-- First, get the organizer and venue IDs

INSERT INTO [Events] (
    Title, Description, Date, Location, Price, Capacity, ImageUrl, 
    IsActive, SeatSelectionMode, VenueId, OrganizerId, CreatedAt, UpdatedAt
)
VALUES (
    'Test Payment Event',
    'A test event for validating Stripe payment integration with BookingLineItems',
    DATEADD(day, 7, GETDATE()),
    'Test Venue',
    25.00,
    100,
    '/events/test.jpg',
    1,
    0, -- GeneralAdmission
    1, -- First venue ID
    1, -- First organizer ID
    GETDATE(),
    GETDATE()
);

-- Get the newly created event ID
DECLARE @EventId INT = SCOPE_IDENTITY();

-- Insert ticket types
INSERT INTO [TicketTypes] (Type, Name, Price, Description, EventId, Color)
VALUES 
    ('General Admission', 'GA Ticket', 25.00, 'General admission ticket', @EventId, '#4CAF50'),
    ('VIP', 'VIP Ticket', 50.00, 'VIP access with premium seating', @EventId, '#FF9800');

-- Insert food items
INSERT INTO [FoodItems] (Name, Price, Description, EventId)
VALUES 
    ('Nachos', 8.50, 'Cheesy nachos with jalapeños', @EventId),
    ('Soft Drink', 3.50, 'Cola, Sprite, or Fanta', @EventId);

-- Output the event ID for reference
SELECT @EventId as NewEventId;
