using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A listing on Staybnb. This is the "hub" entity: images, amenities, bookings,
    /// reviews, the check-in process and host applications all hang off it.
    /// </summary>
    public class HostProperty
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public PropertyType PropertyType { get; set; } = PropertyType.Apartment;

        [Required, MaxLength(250)]
        public string Address { get; set; } = string.Empty;

        /// <summary>Stored separately from Address so guests can filter by city.</summary>
        [Required, MaxLength(100)]
        public string City { get; set; } = string.Empty;

        [Precision(18, 2)]
        [Range(1, 1_000_000)]
        public decimal PricePerNight { get; set; }

        /// <summary>Once-off fee added to every booking (part of the TotalPrice formula).</summary>
        [Precision(18, 2)]
        [Range(0, 100_000)]
        public decimal CleaningFee { get; set; }

        [Range(1, 50)]
        public int MaxGuests { get; set; } = 1;

        [Range(0, 50)]
        public int Bedrooms { get; set; } = 1;

        [Range(0, 50)]
        public int Beds { get; set; } = 1;

        [Range(0, 50)]
        public int Bathrooms { get; set; } = 1;

        /// <summary>
        /// Only active properties appear in the guest gallery.
        /// False while the host application is pending; the Host can toggle it after approval.
        /// </summary>
        public bool IsActive { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Foreign key: the Host who owns it
        [Required]
        public string HostId { get; set; } = string.Empty;
        public ApplicationUser Host { get; set; } = null!;

        // Navigation properties
        public ICollection<PropertyImage> Images { get; set; } = new List<PropertyImage>();

        /// <summary>Many-to-many: EF Core creates the AmenityHostProperty join table.</summary>
        public ICollection<Amenity> Amenities { get; set; } = new List<Amenity>();

        public ICollection<Booking> Bookings { get; set; } = new List<Booking>();

        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        public ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();

        public ICollection<HostApplication> HostApplications { get; set; } = new List<HostApplication>();

        /// <summary>One-to-one: each property has (at most) one check-in process.</summary>
        public CheckInProcess? CheckInProcess { get; set; }
    }
}
