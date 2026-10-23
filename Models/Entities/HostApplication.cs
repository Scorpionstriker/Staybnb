using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A request to list a property. A Guest's first approved application turns them into a Host.
    /// The property is created (inactive) when the application is submitted and linked via PropertyId.
    /// </summary>
    public class HostApplication
    {
        public int Id { get; set; }

        public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;

        /// <summary>True when an existing Host submits another property for approval.</summary>
        public bool IsAdditionalProperty { get; set; }

        // ---------- Applicant ----------
        [Required]
        public string ApplicationUserId { get; set; } = string.Empty;
        public ApplicationUser Applicant { get; set; } = null!;

        // ---------- The property being applied for ----------
        public int PropertyId { get; set; }
        public HostProperty Property { get; set; } = null!;

        // ---------- Admin review (filled in on approve / reject) ----------
        public DateTime? ReviewedAt { get; set; }

        public string? ReviewedById { get; set; }
        public ApplicationUser? ReviewedBy { get; set; }

        [MaxLength(500)]
        public string? AdminNotes { get; set; }
    }
}
