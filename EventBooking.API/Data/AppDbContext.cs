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
        public DbSet<BookingLineItem> BookingLineItems { get; set; }
        public DbSet<SeatReservation> SeatReservations { get; set; } // ✅ Industry-standard reservation tracking
        public DbSet<Venue> Venues { get; set; }
        public DbSet<EventBookingRecord> EventBookings { get; set; } // ✅ Direct ticket booking for organizers

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

            // Configure BookingLineItems relationships
            modelBuilder.Entity<BookingLineItem>(entity =>
            {
                entity.Property(bli => bli.ItemType)
                    .HasMaxLength(20)
                    .IsRequired();

                entity.Property(bli => bli.ItemName)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.Property(bli => bli.UnitPrice)
                    .HasPrecision(18, 2);

                entity.Property(bli => bli.TotalPrice)
                    .HasPrecision(18, 2);

                entity.Property(bli => bli.QRCode)
                    .HasMaxLength(500);

                entity.Property(bli => bli.Status)
                    .HasMaxLength(50)
                    .HasDefaultValue("Active");

                entity.HasOne(bli => bli.Booking)
                    .WithMany(b => b.BookingLineItems)
                    .HasForeignKey(bli => bli.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Create indexes for performance
                entity.HasIndex(bli => bli.BookingId)
                    .HasDatabaseName("IX_BookingLineItems_BookingId");

                entity.HasIndex(bli => new { bli.ItemType, bli.ItemId })
                    .HasDatabaseName("IX_BookingLineItems_ItemType_ItemId");
            });

            // Configure Venue relationships
            modelBuilder.Entity<Event>()
                .HasOne(e => e.Venue)
                .WithMany(v => v.Events)
                .HasForeignKey(e => e.VenueId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure TicketType relationships
            modelBuilder.Entity<TicketType>(entity =>
            {
                entity.Property(t => t.Color)
                    .HasMaxLength(7)
                    .HasDefaultValue("#007bff");

                entity.Property(t => t.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(t => t.Type)
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(t => t.Price)
                    .HasPrecision(18, 2);

                entity.HasOne(tt => tt.Event)
                    .WithMany(e => e.TicketTypes)
                    .HasForeignKey(tt => tt.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
            });            // Configure Seat relationships
            modelBuilder.Entity<Seat>(entity =>
            {
                entity.Property(s => s.Row)
                    .HasMaxLength(10)
                    .IsRequired();
                entity.Property(s => s.SeatNumber)
                    .HasMaxLength(20)
                    .IsRequired();
                entity.HasOne(s => s.Event)
                    .WithMany(e => e.Seats)
                    .HasForeignKey(s => s.EventId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(s => s.TicketType)
                    .WithMany(tt => tt.Seats)
                    .HasForeignKey(s => s.TicketTypeId)
                    .OnDelete(DeleteBehavior.Restrict)
                    .IsRequired();
            });
            
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

            // Configure decimal precision
            modelBuilder.Entity<Event>()
                .Property(e => e.Price)
                .HasPrecision(18, 2);

            // Configure processing fee precision
            modelBuilder.Entity<Event>()
                .Property(e => e.ProcessingFeePercentage)
                .HasPrecision(5, 4); // Allows up to 9.9999% (4 decimal places)

            modelBuilder.Entity<Event>()
                .Property(e => e.ProcessingFeeFixedAmount)
                .HasPrecision(18, 2); // Standard currency precision

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
                .HasPrecision(18, 2);            modelBuilder.Entity<Table>()
                .Property(t => t.TablePrice)
                .HasPrecision(18, 2);

            // ✅ Configure SeatReservation entity for industry-standard reservation tracking
            modelBuilder.Entity<SeatReservation>()
                .HasOne(sr => sr.Event)
                .WithMany()
                .HasForeignKey(sr => sr.EventId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure relationship with Seat
            modelBuilder.Entity<SeatReservation>()
                .HasOne(sr => sr.Seat)
                .WithMany()
                .HasForeignKey(sr => sr.SeatId)
                .OnDelete(DeleteBehavior.Restrict);

            // Add indexes for performance
            modelBuilder.Entity<SeatReservation>()
                .HasIndex(sr => new { sr.EventId, sr.SessionId });

            modelBuilder.Entity<SeatReservation>()
                .HasIndex(sr => new { sr.ExpiresAt, sr.IsConfirmed });

            modelBuilder.Entity<SeatReservation>()
                .HasIndex(sr => new { sr.ReservationId });

            modelBuilder.Entity<SeatReservation>()
                .HasIndex(sr => new { sr.SeatId });

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
