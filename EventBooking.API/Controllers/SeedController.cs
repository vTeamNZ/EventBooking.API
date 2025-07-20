using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace EventBooking.API.Controllers
{
    [Authorize(Roles = "Admin")] // ✅ PRODUCTION SECURITY: Only admins can access seeding endpoints
    [Route("[controller]")]
    [ApiController]
    public class SeedController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SeedController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("venues-and-events")]
        public async Task<ActionResult> SeedVenuesAndEvents()
        {
            try
            {
                // Check if venues already exist
                if (await _context.Venues.AnyAsync())
                {
                    return BadRequest("Seed data already exists");
                }

                // Create venues
                var theaterVenue = new Venue
                {
                    Name = "Grand Theater",
                    Description = "Classic theater with tiered seating",
                    Width = 800,
                    Height = 600
                };

                var restaurantVenue = new Venue
                {
                    Name = "Elegant Dining Hall",
                    Description = "Fine dining venue with table seating",
                    Width = 600,
                    Height = 500
                };

                _context.Venues.AddRange(theaterVenue, restaurantVenue);
                await _context.SaveChangesAsync();

                // Create sections for theater
                // Create ticket types instead of sections
                await _context.SaveChangesAsync();

                // Create demo events
                var theaterEvent = new Event
                {
                    Title = "Shakespeare's Hamlet",
                    Description = "Classic theater performance with assigned seating",
                    Date = DateTime.Now.AddDays(30),
                    Location = "Grand Theater",
                    Price = 75.00m,
                    Capacity = 150,
                    ImageUrl = "/events/1.jpg",
                    IsActive = true,
                    SeatSelectionMode = SeatSelectionMode.EventHall,
                    VenueId = theaterVenue.Id,
                    StagePosition = JsonSerializer.Serialize(new { x = 300, y = 50, width = 200, height = 40 })
                };

                var festivalEvent = new Event
                {
                    Title = "Summer Music Festival",
                    Description = "General admission outdoor music festival",
                    Date = DateTime.Now.AddDays(45),
                    Location = "Festival Grounds",
                    Price = 65.00m,
                    Capacity = 1000,
                    ImageUrl = "/events/2.jpg",
                    IsActive = true,
                    SeatSelectionMode = SeatSelectionMode.GeneralAdmission
                };

                var concertEvent = new Event
                {
                    Title = "Live Music Concert",
                    Description = "General admission standing room concert",
                    Date = DateTime.Now.AddDays(60),
                    Location = "Concert Hall",
                    Price = 50.00m,
                    Capacity = 500,
                    ImageUrl = "/events/3.jpg",
                    IsActive = true,
                    SeatSelectionMode = SeatSelectionMode.GeneralAdmission
                };
                
                var diningEvent = new Event
                {
                    Title = "Gala Dinner",
                    Description = "Elegant dining experience with table seating",
                    Date = DateTime.Now.AddDays(50),
                    Location = "Elegant Dining Hall",
                    Price = 120.00m,
                    Capacity = 100,
                    ImageUrl = "/events/4.jpg",
                    IsActive = true,
                    SeatSelectionMode = SeatSelectionMode.EventHall,
                    VenueId = restaurantVenue.Id,
                    StagePosition = JsonSerializer.Serialize(new { x = 250, y = 40, width = 100, height = 30 })
                };

                _context.Events.AddRange(theaterEvent, festivalEvent, concertEvent, diningEvent);
                await _context.SaveChangesAsync();

                // Create ticket types for theater event
                var vipTicketType = new TicketType
                {
                    EventId = theaterEvent.Id,
                    Type = "VIP",
                    Name = "VIP",
                    Color = "#FFD700",
                    Price = 120.00m,
                    Description = "VIP seating with best view"
                };

                var premiumTicketType = new TicketType
                {
                    EventId = theaterEvent.Id,
                    Type = "Premium",
                    Name = "Premium",
                    Color = "#FFA500",
                    Price = 90.00m,
                    Description = "Premium seating with good view"
                };

                var generalTicketType = new TicketType
                {
                    EventId = theaterEvent.Id,
                    Type = "General",
                    Name = "General",
                    Color = "#87CEEB",
                    Price = 60.00m,
                    Description = "General admission seating"
                };

                // Create ticket types for dining event
                var frontTicketType = new TicketType
                {
                    EventId = diningEvent.Id,
                    Type = "Front Tables",
                    Name = "Front Tables",
                    Color = "#FF69B4",
                    Price = 150.00m,
                    Description = "Front table seating"
                };

                var backTicketType = new TicketType
                {
                    EventId = diningEvent.Id,
                    Type = "Back Tables",
                    Name = "Back Tables",
                    Color = "#DDA0DD",
                    Price = 120.00m,
                    Description = "Back table seating"
                };

                _context.TicketTypes.AddRange(vipTicketType, premiumTicketType, generalTicketType, frontTicketType, backTicketType);
                await _context.SaveChangesAsync();

                // Create seats for theater event
                var theaterSeats = new List<Seat>();
                var seatId = 1;

                // VIP section (front 3 rows)
                for (int row = 1; row <= 3; row++)
                {
                    for (int seat = 1; seat <= 10; seat++)
                    {
                        theaterSeats.Add(new Seat
                        {
                            EventId = theaterEvent.Id,
                            TicketTypeId = vipTicketType.Id,
                            Row = ((char)('A' + row - 1)).ToString(),
                            Number = seat,
                            SeatNumber = $"{(char)('A' + row - 1)}{seat}",
                            X = 50 + (seat - 1) * 35,
                            Y = 100 + (row - 1) * 40,
                            Width = 30,
                            Height = 35,
                            Price = vipTicketType.Price,
                            Status = SeatStatus.Available
                        });
                    }
                }

                // Premium section (middle 4 rows)
                for (int row = 4; row <= 7; row++)
                {
                    for (int seat = 1; seat <= 12; seat++)
                    {
                        theaterSeats.Add(new Seat
                        {
                            EventId = theaterEvent.Id,
                            TicketTypeId = premiumTicketType.Id,
                            Row = ((char)('A' + row - 1)).ToString(),
                            Number = seat,
                            SeatNumber = $"{(char)('A' + row - 1)}{seat}",
                            X = 40 + (seat - 1) * 35,
                            Y = 100 + (row - 1) * 40,
                            Width = 30,
                            Height = 35,
                            Price = premiumTicketType.Price,
                            Status = SeatStatus.Available
                        });
                    }
                }

                // General section (back 5 rows)
                for (int row = 8; row <= 12; row++)
                {
                    for (int seat = 1; seat <= 14; seat++)
                    {
                        theaterSeats.Add(new Seat
                        {
                            EventId = theaterEvent.Id,
                            TicketTypeId = generalTicketType.Id,
                            Row = ((char)('A' + row - 1)).ToString(),
                            Number = seat,
                            SeatNumber = $"{(char)('A' + row - 1)}{seat}",
                            X = 30 + (seat - 1) * 35,
                            Y = 100 + (row - 1) * 40,
                            Width = 30,
                            Height = 35,
                            Price = generalTicketType.Price,
                            Status = SeatStatus.Available
                        });
                    }
                }

                _context.Seats.AddRange(theaterSeats);

                // Create tables for dining event
                var tables = new List<Table>();
                var tableSeats = new List<Seat>();

                // Front tables (premium)
                for (int table = 1; table <= 6; table++)
                {
                    var newTable = new Table
                    {
                        EventId = diningEvent.Id,
                        TableNumber = $"F{table}",
                        Capacity = 8,
                        X = 50 + ((table - 1) % 3) * 150,
                        Y = 100 + ((table - 1) / 3) * 120,
                        Width = 100,
                        Height = 80,
                        Shape = "round",
                        PricePerSeat = frontTicketType.Price
                    };
                    tables.Add(newTable);
                }

                // Back tables (standard)
                for (int table = 1; table <= 8; table++)
                {
                    var newTable = new Table
                    {
                        EventId = diningEvent.Id,
                        TableNumber = $"B{table}",
                        Capacity = 6,
                        X = 50 + ((table - 1) % 4) * 120,
                        Y = 300 + ((table - 1) / 4) * 100,
                        Width = 80,
                        Height = 60,
                        Shape = "round",
                        PricePerSeat = backTicketType.Price
                    };
                    tables.Add(newTable);
                }

                _context.Tables.AddRange(tables);
                await _context.SaveChangesAsync();

                // Create seats for tables
                foreach (var table in tables)
                {
                    for (int seatNum = 1; seatNum <= table.Capacity; seatNum++)
                    {
                        tableSeats.Add(new Seat
                        {
                            EventId = diningEvent.Id,
                            TableId = table.Id,
                            TicketTypeId = table.Seats.Any() ? table.Seats.First().TicketTypeId : null,
                            Row = table.TableNumber,
                            Number = seatNum,
                            SeatNumber = $"{table.TableNumber}-{seatNum}",
                            X = table.X + (seatNum % 4) * 20,
                            Y = table.Y + (seatNum / 4) * 20,
                            Width = 15,
                            Height = 15,
                            Price = table.PricePerSeat,
                            Status = SeatStatus.Available
                        });
                    }
                }

                _context.Seats.AddRange(tableSeats);

                // Create ticket types for general admission events
                var ticketTypes = new List<TicketType>();

                // General admission events ticket types
                // Concert event
                ticketTypes.AddRange(new[]
                {
                    new TicketType
                    {
                        EventId = concertEvent.Id,
                        Type = "Adult",
                        Price = 50.00m,
                        Description = "General admission for adults",
                        Color = "#9F7AEA" // Purple color for adult tickets
                    },
                    new TicketType
                    {
                        EventId = concertEvent.Id,
                        Type = "Student",
                        Price = 35.00m,
                        Description = "Discounted price for students with valid ID",
                        Color = "#48BB78" // Green color for student tickets
                    },
                    new TicketType
                    {
                        EventId = concertEvent.Id,
                        Type = "Child",
                        Price = 25.00m,
                        Description = "For children under 12",
                        Color = "#ED8936" // Orange color for child tickets
                    }
                });

                // Festival event
                ticketTypes.AddRange(new[]
                {
                    new TicketType
                    {
                        EventId = festivalEvent.Id,
                        Type = "Adult",
                        Price = 65.00m,
                        Description = "General admission for adults",
                        Color = "#F56565" // Red color for adult festival tickets
                    },
                    new TicketType
                    {
                        EventId = festivalEvent.Id,
                        Type = "Student",
                        Price = 45.00m,
                        Description = "Discounted price for students with valid ID",
                        Color = "#38B2AC" // Teal color for student festival tickets
                    },
                    new TicketType
                    {
                        EventId = festivalEvent.Id,
                        Type = "Child",
                        Price = 30.00m,
                        Description = "For children under 12",
                        Color = "#D69E2E" // Yellow color for child festival tickets
                    }
                });

                _context.TicketTypes.AddRange(ticketTypes);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Demo data created successfully",
                    venues = 2,
                    events = 3,
                    seats = theaterSeats.Count + tableSeats.Count,
                    tables = tables.Count,
                    ticketTypes = ticketTypes.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error creating seed data", error = ex.Message });
            }
        }

        [HttpDelete("clear-all")]
        public async Task<ActionResult> ClearAllData()
        {
            try
            {
                // Clear in order due to foreign key constraints
                _context.Seats.RemoveRange(_context.Seats);
                _context.Tables.RemoveRange(_context.Tables);
                _context.TicketTypes.RemoveRange(_context.TicketTypes);
                _context.Events.RemoveRange(_context.Events);
                _context.TicketTypes.RemoveRange(_context.TicketTypes);
                _context.Venues.RemoveRange(_context.Venues);

                await _context.SaveChangesAsync();

                return Ok(new { message = "All data cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clearing data", error = ex.Message });
            }
        }

        [HttpPost("update-ticket-colors")]
        public async Task<ActionResult> UpdateTicketTypeColors()
        {
            try
            {
                var ticketTypes = await _context.TicketTypes.ToListAsync();
                
                foreach (var ticket in ticketTypes)
                {
                    // Update colors based on ticket type
                    switch (ticket.Type.ToLower())
                    {
                        case "vip":
                            ticket.Color = "#FFD700"; // Gold
                            break;
                        case "premium":
                            ticket.Color = "#C0C0C0"; // Silver
                            break;
                        case "general":
                            ticket.Color = "#CD7F32"; // Bronze
                            break;
                        case "front section":
                            ticket.Color = "#FF6B6B"; // Red
                            break;
                        case "back section":
                            ticket.Color = "#4ECDC4"; // Teal
                            break;
                        case "adult":
                            // Use different colors for different events
                            if (ticket.Price == 50.00m) // Concert
                                ticket.Color = "#9F7AEA"; // Purple
                            else if (ticket.Price == 65.00m) // Festival
                                ticket.Color = "#F56565"; // Red
                            break;
                        case "student":
                            if (ticket.Price == 35.00m) // Concert
                                ticket.Color = "#48BB78"; // Green
                            else if (ticket.Price == 45.00m) // Festival
                                ticket.Color = "#38B2AC"; // Teal
                            break;
                        case "child":
                            if (ticket.Price == 25.00m) // Concert
                                ticket.Color = "#ED8936"; // Orange
                            else if (ticket.Price == 30.00m) // Festival
                                ticket.Color = "#D69E2E"; // Yellow
                            break;
                        default:
                            ticket.Color = "#3B82F6"; // Default blue
                            break;
                    }
                }
                
                await _context.SaveChangesAsync();
                
                return Ok(new
                {
                    message = "Ticket type colors updated successfully",
                    updatedCount = ticketTypes.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest($"Error updating ticket colors: {ex.Message}");
            }
        }

        // REMOVED: Test event creation endpoint - Not needed in production
        // [HttpPost("test-event")]
        // This endpoint allowed creating test events and should only be available in development

    }
}
