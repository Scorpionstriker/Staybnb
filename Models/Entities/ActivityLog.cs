using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// Audit trail: every login, role change and booking status update is written here.
    /// UserId is nullable so system events and failed logins (unknown user) can be logged too.
    /// </summary>
    public class ActivityLog
    {
        public int Id { get; set; }

        /// <summary>Short machine-friendly action name, e.g. "HostApplicationApproved".</summary>
        [Required, MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        public ActivityType ActivityType { get; set; } = ActivityType.System;

        /// <summary>Human-readable description, e.g. "Booking #12 changed from Pending to Approved".</summary>
        [MaxLength(500)]
        public string? Details { get; set; }

        [MaxLength(45)]
        public string? IpAddress { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? UserId { get; set; }
        public ApplicationUser? User { get; set; }
    }
}
