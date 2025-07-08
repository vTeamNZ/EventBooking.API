using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using EventBooking.API.Models;
using EventBooking.API.Models.Payment;
using EventBooking.API.Models.Payments;

namespace EventBooking.API.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<Event> Events { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Organizer> Organizers { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }
        public DbSet<Seat> Seats { get; set; }
        public DbSet<Table> Tables { get; set; }
        public DbSet<TableReservation> TableReservations { get; set; }
        public DbSet<TicketType> TicketTypes { get; set; }
        public DbSet<FoodItem> FoodItems { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<BookingTicket> BookingTickets { get; set; }
        public DbSet<BookingFood> BookingFoods { get; set; }
        public DbSet<SeatReservation> SeatReservations { get; set; }
        
        // New entities for seat selection
        public DbSet<Venue> Venues { get; set; }
        public DbSet<Section> Sections { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure Payment entity
            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // Configure PaymentRecord entity
            modelBuilder.Entity<PaymentRecord>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            // Configure Booking relationships
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Event)
                .WithMany()
                .HasForeignKey(b => b.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Venue relationships
            modelBuilder.Entity<Event>()
                .HasOne(e => e.Venue)
                .WithMany(v => v.Events)
                .HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure Section relationships
            modelBuilder.Entity<Section>()
                .HasOne(s => s.Venue)
                .WithMany(v => v.Sections)
                .HasForeignKey(s => s.VenueId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Seat relationships
            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Event)
                .WithMany(e => e.Seats)
                .HasForeignKey(s => s.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Section)
                .WithMany(sec => sec.Seats)
                .HasForeignKey(s => s.SectionId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Seat>()
                .HasOne(s => s.Table)
                .WithMany(t => t.Seats)
                .HasForeignKey(s => s.TableId)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure Table relationships
            modelBuilder.Entity<Table>()
                .HasOne(t => t.Event)
                .WithMany(e => e.Tables)
                .HasForeignKey(t => t.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Table>()
                .HasOne(t => t.Section)
                .WithMany(s => s.Tables)
                .HasForeignKey(t => t.SectionId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure decimal precision
            modelBuilder.Entity<Event>()
                .Property(e => e.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FoodItem>()
                .Property(f => f.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<TicketType>()
                .Property(t => t.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Seat>()
                .Property(s => s.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Seat>()
                .Property(s => s.X)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Seat>()
                .Property(s => s.Y)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Seat>()
                .Property(s => s.Width)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Seat>()
                .Property(s => s.Height)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Table>()
                .Property(t => t.X)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Table>()
                .Property(t => t.Y)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Table>()
                .Property(t => t.Width)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Table>()
                .Property(t => t.Height)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Table>()
                .Property(t => t.PricePerSeat)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Table>()
                .Property(t => t.TablePrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Section>()
                .Property(s => s.BasePrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BookingTicket>()
                .HasOne(bt => bt.Booking)
                .WithMany(b => b.BookingTickets)
                .HasForeignKey(bt => bt.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingTicket>()
                .HasOne(bt => bt.TicketType)
                .WithMany()
                .HasForeignKey(bt => bt.TicketTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookingFood>()
                .HasOne(bf => bf.Booking)
                .WithMany(b => b.BookingFoods)
                .HasForeignKey(bf => bf.BookingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BookingFood>()
                .HasOne(bf => bf.FoodItem)
                .WithMany()
                .HasForeignKey(bf => bf.FoodItemId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SeatReservation
            modelBuilder.Entity<SeatReservation>()
                .HasOne(sr => sr.Event)
                .WithMany()
                .HasForeignKey(sr => sr.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<SeatReservation>()
                .HasIndex(sr => new { sr.EventId, sr.Row, sr.Number })
                .IsUnique();

            // Add composite index for looking up active reservations
            modelBuilder.Entity<SeatReservation>()
                .HasIndex(sr => new { sr.EventId, sr.IsConfirmed, sr.ExpiresAt });

            // Optional: Ensure delete behaviors and FK relationship
            modelBuilder.Entity<Organizer>()
                .HasOne(o => o.User)
                .WithOne()
                .HasForeignKey<Organizer>(o => o.UserId);

            // Configure Reservation relationships explicitly to fix shadow property issue
            modelBuilder.Entity<Reservation>()
                .HasOne<ApplicationUser>(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Event)
                .WithMany()
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade);

        }

    }
}
