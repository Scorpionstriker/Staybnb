using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A photo of a property. The file itself lives in wwwroot/uploads/properties;
    /// only its URL is stored in the database.
    /// </summary>
    public class PropertyImage
    {
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>The main photo shown on the gallery card.</summary>
        public bool IsCover { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public int PropertyId { get; set; }
        public HostProperty Property { get; set; } = null!;
    }
}
