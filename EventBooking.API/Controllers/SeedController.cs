using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Data;
using EventBooking.API.Models;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;

namespace EventBooking.API.Controllers
{
    // Restore proper Admin authorization for seeding in production
    // Comment this line for testing if needed
    [Authorize(Roles = "Admin")]
    [Route("api/[controller]")]
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
                var vipSection = new Section
                {
                    Name = "VIP",
                    Color = "#FFD700",
                    BasePrice = 150.00m,
                    VenueId = theaterVenue.Id
                };

                var premiumSection = new Section
                {
                    Name = "Premium",
                    Color = "#C0C0C0",
                    BasePrice = 100.00m,
                    VenueId = theaterVenue.Id
                };

                var generalSection = new Section
                {
                    Name = "General",
                    Color = "#CD7F32",
                    BasePrice = 75.00m,
                    VenueId = theaterVenue.Id
                };

                // Create sections for restaurant
                var frontSection = new Section
                {
                    Name = "Front Tables",
                    Color = "#FF6B6B",
                    BasePrice = 120.00m,
                    VenueId = restaurantVenue.Id
                };

                var backSection = new Section
                {
                    Name = "Back Tables",
                    Color = "#4ECDC4",
                    BasePrice = 90.00m,
                    VenueId = restaurantVenue.Id
                };

                _context.Sections.AddRange(vipSection, premiumSection, generalSection, frontSection, backSection);
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

                var diningEvent = new Event
                {
                    Title = "Wine Tasting Dinner",
                    Description = "Elegant dining experience with table reservations",
                    Date = DateTime.Now.AddDays(45),
                    Location = "Elegant Dining Hall",
                    Price = 90.00m,
                    Capacity = 80,
                    ImageUrl = "/events/2.jpg",
                    IsActive = true,
                    SeatSelectionMode = SeatSelectionMode.TableSeating,
                    VenueId = restaurantVenue.Id
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

                _context.Events.AddRange(theaterEvent, diningEvent, concertEvent);
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
                            SectionId = vipSection.Id,
                            Row = ((char)('A' + row - 1)).ToString(),
                            Number = seat,
                            SeatNumber = $"{(char)('A' + row - 1)}{seat}",
                            X = 50 + (seat - 1) * 35,
                            Y = 100 + (row - 1) * 40,
                            Width = 30,
                            Height = 35,
                            Price = vipSection.BasePrice,
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
                            SectionId = premiumSection.Id,
                            Row = ((char)('A' + row - 1)).ToString(),
                            Number = seat,
                            SeatNumber = $"{(char)('A' + row - 1)}{seat}",
                            X = 40 + (seat - 1) * 35,
                            Y = 100 + (row - 1) * 40,
                            Width = 30,
                            Height = 35,
                            Price = premiumSection.BasePrice,
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
                            SectionId = generalSection.Id,
                            Row = ((char)('A' + row - 1)).ToString(),
                            Number = seat,
                            SeatNumber = $"{(char)('A' + row - 1)}{seat}",
                            X = 30 + (seat - 1) * 35,
                            Y = 100 + (row - 1) * 40,
                            Width = 30,
                            Height = 35,
                            Price = generalSection.BasePrice,
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
                        SectionId = frontSection.Id,
                        TableNumber = $"F{table}",
                        Capacity = 8,
                        X = 50 + ((table - 1) % 3) * 150,
                        Y = 100 + ((table - 1) / 3) * 120,
                        Width = 100,
                        Height = 80,
                        Shape = "round",
                        PricePerSeat = frontSection.BasePrice
                    };
                    tables.Add(newTable);
                }

                // Back tables (standard)
                for (int table = 1; table <= 8; table++)
                {
                    var newTable = new Table
                    {
                        EventId = diningEvent.Id,
                        SectionId = backSection.Id,
                        TableNumber = $"B{table}",
                        Capacity = 6,
                        X = 50 + ((table - 1) % 4) * 120,
                        Y = 300 + ((table - 1) / 4) * 100,
                        Width = 80,
                        Height = 60,
                        Shape = "round",
                        PricePerSeat = backSection.BasePrice
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
                            SectionId = table.SectionId,
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

                // Create ticket types for general admission event
                var ticketTypes = new List<TicketType>
                {
                    new TicketType
                    {
                        EventId = concertEvent.Id,
                        Type = "Adult",
                        Price = 50.00m,
                        Description = "General admission for adults"
                    },
                    new TicketType
                    {
                        EventId = concertEvent.Id,
                        Type = "Student",
                        Price = 35.00m,
                        Description = "Discounted price for students with valid ID"
                    },
                    new TicketType
                    {
                        EventId = concertEvent.Id,
                        Type = "Child",
                        Price = 25.00m,
                        Description = "For children under 12"
                    }
                };

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
                _context.Sections.RemoveRange(_context.Sections);
                _context.Venues.RemoveRange(_context.Venues);

                await _context.SaveChangesAsync();

                return Ok(new { message = "All data cleared successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error clearing data", error = ex.Message });
            }
        }
    }
}
