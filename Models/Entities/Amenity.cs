using System.ComponentModel.DataAnnotations;

namespace Staybnb.Models.Entities
{
    /// <summary>
    /// A feature a property can offer (Wifi, Kitchen, Pool...).
    /// IconClass holds a Bootstrap Icons class name, e.g. "bi-wifi".
    /// </summary>
    public class Amenity
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(50)]
        public string IconClass { get; set; } = string.Empty;

        public ICollection<HostProperty> Properties { get; set; } = new List<HostProperty>();
    }
}
