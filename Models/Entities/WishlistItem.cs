using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A property a user has saved (the heart icon). A user can save a property only once.
    /// </summary>
    public class WishlistItem
    {
        public int Id { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public int PropertyId { get; set; }
        public HostProperty Property { get; set; } = null!;
    }
}
