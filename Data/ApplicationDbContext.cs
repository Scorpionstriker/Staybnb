using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Data
{
    /// <summary>
    /// The EF Core database context. Inheriting from IdentityDbContext&lt;ApplicationUser&gt;
    /// gives us the AspNetUsers / AspNetRoles / AspNetUserRoles tables automatically.
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // ---------- Tables ----------
        public DbSet<HostProperty> HostProperties => Set<HostProperty>();
        public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
        public DbSet<Amenity> Amenities => Set<Amenity>();
        public DbSet<HostApplication> HostApplications => Set<HostApplication>();
        public DbSet<CheckInProcess> CheckInProcesses => Set<CheckInProcess>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<GuestCheckIn> GuestCheckIns => Set<GuestCheckIn>();
        public DbSet<GuestDocument> GuestDocuments => Set<GuestDocument>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

        /// <summary>
        /// Store every enum as readable text ("Pending", "Approved") instead of numbers,
        /// so the tables are easy to read in SQL Server Object Explorer.
        /// </summary>
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<BookingStatus>().HaveConversion<string>().HaveMaxLength(30);
            configurationBuilder.Properties<ApplicationStatus>().HaveConversion<string>().HaveMaxLength(30);
            configurationBuilder.Properties<PaymentStatus>().HaveConversion<string>().HaveMaxLength(30);
            configurationBuilder.Properties<CheckInStatus>().HaveConversion<string>().HaveMaxLength(30);
            configurationBuilder.Properties<DocumentStatus>().HaveConversion<string>().HaveMaxLength(30);
            configurationBuilder.Properties<ActivityType>().HaveConversion<string>().HaveMaxLength(40);
            configurationBuilder.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(40);
            configurationBuilder.Properties<PropertyType>().HaveConversion<string>().HaveMaxLength(30);
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // REQUIRED: builds all the Identity tables. Must be called first.
            base.OnModelCreating(builder);

            // =====================================================================
            // DELETE RULES
            // SQL Server refuses "multiple cascade paths" to the same table.
            // Rule used here: cascade from a property/booking down to its own
            // children, and RESTRICT on foreign keys that point at users.
            // (Properties are deactivated, not deleted, once they have bookings.)
            // =====================================================================

            // ---------- HostProperty ----------
            builder.Entity<HostProperty>(e =>
            {
                e.HasOne(p => p.Host)
                 .WithMany(u => u.Properties)
                 .HasForeignKey(p => p.HostId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Many-to-many with Amenity -> EF creates the "AmenityHostProperty" join table
                e.HasMany(p => p.Amenities)
                 .WithMany(a => a.Properties);

                e.HasIndex(p => p.City);
                e.HasIndex(p => p.IsActive);
            });

            // ---------- PropertyImage (1 property : many images) ----------
            builder.Entity<PropertyImage>()
                .HasOne(i => i.Property)
                .WithMany(p => p.Images)
                .HasForeignKey(i => i.PropertyId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- CheckInProcess (1 property : 1 process) ----------
            builder.Entity<CheckInProcess>(e =>
            {
                e.HasOne(c => c.Property)
                 .WithOne(p => p.CheckInProcess)
                 .HasForeignKey<CheckInProcess>(c => c.PropertyId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(c => c.PropertyId).IsUnique();
            });

            // ---------- HostApplication ----------
            builder.Entity<HostApplication>(e =>
            {
                e.HasOne(h => h.Applicant)
                 .WithMany(u => u.HostApplications)
                 .HasForeignKey(h => h.ApplicationUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Second link to users (the Admin who reviewed it) - no collection on the user side
                e.HasOne(h => h.ReviewedBy)
                 .WithMany()
                 .HasForeignKey(h => h.ReviewedById)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(h => h.Property)
                 .WithMany(p => p.HostApplications)
                 .HasForeignKey(h => h.PropertyId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(h => h.Status);
            });

            // ---------- Booking ----------
            builder.Entity<Booking>(e =>
            {
                e.HasOne(b => b.Guest)
                 .WithMany(u => u.Bookings)
                 .HasForeignKey(b => b.GuestId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Keep booking history: a property with bookings cannot be deleted
                e.HasOne(b => b.Property)
                 .WithMany(p => p.Bookings)
                 .HasForeignKey(b => b.PropertyId)
                 .OnDelete(DeleteBehavior.Restrict);

                // Store dates without a time part
                e.Property(b => b.CheckInDate).HasColumnType("date");
                e.Property(b => b.CheckOutDate).HasColumnType("date");

                // Speeds up the "is this property already booked for these dates?" check
                e.HasIndex(b => new { b.PropertyId, b.CheckInDate, b.CheckOutDate });
            });

            // ---------- Payment (1 booking : 1 payment) ----------
            builder.Entity<Payment>(e =>
            {
                e.HasOne(p => p.Booking)
                 .WithOne(b => b.Payment)
                 .HasForeignKey<Payment>(p => p.BookingId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(p => p.BookingId).IsUnique();
            });

            // ---------- GuestCheckIn (1 booking : 1 check-in) ----------
            builder.Entity<GuestCheckIn>(e =>
            {
                e.HasOne(g => g.Booking)
                 .WithOne(b => b.GuestCheckIn)
                 .HasForeignKey<GuestCheckIn>(g => g.BookingId)
                 .OnDelete(DeleteBehavior.Cascade);

                // Second path to the same check-in -> must be Restrict
                e.HasOne(g => g.CheckInProcess)
                 .WithMany(c => c.GuestCheckIns)
                 .HasForeignKey(g => g.CheckInProcessId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(g => g.BookingId).IsUnique();
            });

            // ---------- GuestDocument (1 check-in : many documents) ----------
            builder.Entity<GuestDocument>()
                .HasOne(d => d.GuestCheckIn)
                .WithMany(g => g.Documents)
                .HasForeignKey(d => d.GuestCheckInId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- Review ----------
            builder.Entity<Review>(e =>
            {
                e.HasOne(r => r.Reviewer)
                 .WithMany(u => u.Reviews)
                 .HasForeignKey(r => r.ReviewerId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(r => r.Property)
                 .WithMany(p => p.Reviews)
                 .HasForeignKey(r => r.PropertyId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // ---------- WishlistItem ----------
            builder.Entity<WishlistItem>(e =>
            {
                e.HasOne(w => w.User)
                 .WithMany(u => u.WishlistItems)
                 .HasForeignKey(w => w.UserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(w => w.Property)
                 .WithMany(p => p.WishlistItems)
                 .HasForeignKey(w => w.PropertyId)
                 .OnDelete(DeleteBehavior.Cascade);

                // A user can save a property only once
                e.HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();
            });

            // ---------- Message (two links to the users table) ----------
            builder.Entity<Message>(e =>
            {
                e.HasOne(m => m.Sender)
                 .WithMany(u => u.SentMessages)
                 .HasForeignKey(m => m.SenderId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(m => m.Receiver)
                 .WithMany(u => u.ReceivedMessages)
                 .HasForeignKey(m => m.ReceiverId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(m => m.Property)
                 .WithMany()
                 .HasForeignKey(m => m.PropertyId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(m => new { m.ReceiverId, m.IsRead });
            });

            // ---------- Notification ----------
            builder.Entity<Notification>(e =>
            {
                e.HasOne(n => n.User)
                 .WithMany(u => u.Notifications)
                 .HasForeignKey(n => n.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasIndex(n => new { n.UserId, n.IsRead });
            });

            // ---------- ActivityLog (keep logs even if the user is deleted) ----------
            builder.Entity<ActivityLog>(e =>
            {
                e.HasOne(a => a.User)
                 .WithMany(u => u.ActivityLogs)
                 .HasForeignKey(a => a.UserId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(a => a.CreatedAt);
            });

            // =====================================================================
            // SEED DATA: amenities (fixed lookup data, so HasData is ideal).
            // IconClass values are Bootstrap Icons class names.
            // =====================================================================
            builder.Entity<Amenity>().HasData(
                new Amenity { Id = 1, Name = "Wifi", IconClass = "bi-wifi" },
                new Amenity { Id = 2, Name = "Kitchen", IconClass = "bi-cup-hot" },
                new Amenity { Id = 3, Name = "Washer", IconClass = "bi-basket" },
                new Amenity { Id = 4, Name = "Dryer", IconClass = "bi-wind" },
                new Amenity { Id = 5, Name = "Air Conditioning", IconClass = "bi-snow" },
                new Amenity { Id = 6, Name = "Heating", IconClass = "bi-thermometer-sun" },
                new Amenity { Id = 7, Name = "Free Parking", IconClass = "bi-p-square" },
                new Amenity { Id = 8, Name = "Pool", IconClass = "bi-water" },
                new Amenity { Id = 9, Name = "TV", IconClass = "bi-tv" },
                new Amenity { Id = 10, Name = "Workspace", IconClass = "bi-laptop" },
                new Amenity { Id = 11, Name = "Braai Area", IconClass = "bi-fire" },
                new Amenity { Id = 12, Name = "Backup Power", IconClass = "bi-lightning-charge" }
            );
        }
    }
}
