using System.ComponentModel.DataAnnotations;
using Staybnb.Models.Enums;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// An ID or passport uploaded during check-in. The file is stored privately
    /// (outside wwwroot) and only the stored file name is kept here.
    /// </summary>
    public class GuestDocument
    {
        public int Id { get; set; }

        /// <summary>e.g. "ID", "Passport".</summary>
        [Required, MaxLength(100)]
        public string DocumentType { get; set; } = string.Empty;

        /// <summary>The unique name the file was saved under on the server.</summary>
        [Required, MaxLength(200)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>The name of the file on the guest's computer (for display only).</summary>
        [MaxLength(200)]
        public string? OriginalFileName { get; set; }

        [MaxLength(100)]
        public string? ContentType { get; set; }

        public DocumentStatus Status { get; set; } = DocumentStatus.Pending;

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ReviewedAt { get; set; }

        [MaxLength(300)]
        public string? RejectionReason { get; set; }

        public int GuestCheckInId { get; set; }
        public GuestCheckIn GuestCheckIn { get; set; } = null!;
    }
}
