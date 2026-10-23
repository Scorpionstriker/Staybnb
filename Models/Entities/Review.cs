using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A Guest's rating and comment for a property they stayed at.
    /// </summary>
    public class Review
    {
        public int Id { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(1000)]
        public string Comment { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int PropertyId { get; set; }
        public HostProperty Property { get; set; } = null!;

        [Required]
        public string ReviewerId { get; set; } = string.Empty;
        public ApplicationUser Reviewer { get; set; } = null!;
    }
}
