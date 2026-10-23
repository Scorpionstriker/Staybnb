using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models.Enums;

namespace Staybnb.Models.ViewModels.Host
{
    /// <summary>
    /// The "Become a Host" form. Property details + at least one photo.
    /// The same form is used by existing Hosts to submit an additional property.
    /// </summary>
    public class HostApplicationViewModel
    {
        // Step 1: The place
        [Required(ErrorMessage = "Give your place a title.")]
        [StringLength(100, MinimumLength = 5, ErrorMessage = "The title must be between {2} and {1} characters.")]
        [Display(Name = "Listing title")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Describe your place for guests.")]
        [StringLength(4000, MinimumLength = 30, ErrorMessage = "The description must be at least {2} characters.")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Property type")]
        public PropertyType PropertyType { get; set; } = PropertyType.Apartment;

        // Step 2: Location
        [Required(ErrorMessage = "Enter the street address.")]
        [StringLength(250)]
        [Display(Name = "Street address")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Enter the city or town.")]
        [StringLength(100)]
        [Display(Name = "City / town")]
        public string City { get; set; } = string.Empty;

        // Step 3: Rooms
        [Range(1, 50, ErrorMessage = "Guests must be between {1} and {2}.")]
        [Display(Name = "Maximum guests")]
        public int MaxGuests { get; set; } = 2;

        [Range(0, 50)]
        public int Bedrooms { get; set; } = 1;

        [Range(1, 50, ErrorMessage = "At least one bed is required.")]
        public int Beds { get; set; } = 1;

        [Range(0, 50)]
        public int Bathrooms { get; set; } = 1;

        // Step 4: Amenities
        public List<int> SelectedAmenityIds { get; set; } = new();

        // Step 5: Pricing
        [Range(typeof(decimal), "50", "1000000", ErrorMessage = "The nightly price must be between R50 and R1 000 000.")]
        [Display(Name = "Price per night (R)")]
        public decimal PricePerNight { get; set; } = 1000m;

        [Range(typeof(decimal), "0", "100000", ErrorMessage = "The cleaning fee must be between R0 and R100 000.")]
        [Display(Name = "Cleaning fee (R)")]
        public decimal CleaningFee { get; set; }

        // Step 6: Photos (REQUIRED - at least one)
        /// <summary>
        /// Nullable on purpose: the "at least one image" rule is checked explicitly in the
        /// controller/service so we can show a clear message.
        /// </summary>
        [Display(Name = "Photos")]
        public List<IFormFile>? Images { get; set; }

        /// <summary>Must be ticked - checked in the controller (a [Range] on a bool breaks jQuery validation).</summary>
        [Display(Name = "I confirm these details are accurate and I am allowed to host this property.")]
        public bool ConfirmAccuracy { get; set; }

        // Display-only data for the form (never posted back)
        [ValidateNever]
        public List<AmenityOption> AvailableAmenities { get; set; } = new();

        [ValidateNever]
        public bool IsAdditionalProperty { get; set; }

        [ValidateNever]
        public int MaxImages { get; set; } = 10;

        [ValidateNever]
        public int MaxImageSizeMb { get; set; } = 5;
    }

    public class AmenityOption
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string IconClass { get; set; } = string.Empty;
    }
}
