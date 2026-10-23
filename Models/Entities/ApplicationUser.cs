using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// The Staybnb user. Inherits Id, Email, UserName, PasswordHash etc. from IdentityUser.
    /// A user's role (Guest / Host / Admin / SuperAdmin) is stored in AspNetUserRoles, not here.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        [Required, MaxLength(50)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(300)]
        public string? ProfilePictureUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [NotMapped]
        public string FullName => $"{FirstName} {LastName}".Trim();

        // ---------- Navigation properties ----------

        /// <summary>Properties this user hosts (only populated once they are a Host).</summary>
        public ICollection<HostProperty> Properties { get; set; } = new List<HostProperty>();

        /// <summary>Bookings this user made as a Guest.</summary>
        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        /// <summary>Host applications this user submitted.</summary>
        public ICollection<HostApplication> HostApplications { get; set; } = new List<HostApplication>();

        public ICollection<Message> SentMessages { get; set; } = new List<Message>();

        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();

        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        public ICollection<ActivityLog> ActivityLogs { get; set; } = new List<ActivityLog>();
    }
}
