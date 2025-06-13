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
                .OnDelete(DeleteBehavior.Restrict);

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

            // Optional: Ensure delete behaviors and FK relationship
            modelBuilder.Entity<Organizer>()
                .HasOne(o => o.User)
                .WithOne()
                .HasForeignKey<Organizer>(o => o.UserId);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict); // 👈 disables cascade for User

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Event)
                .WithMany()
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Cascade); // Optional: you can leave this one

        }

    }
}
