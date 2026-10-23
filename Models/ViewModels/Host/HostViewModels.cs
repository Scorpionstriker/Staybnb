using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Models.ViewModels.Host
{
    // Dashboard
    public class HostDashboardViewModel
    {
        public string HostFirstName { get; set; } = string.Empty;

        public int TotalProperties { get; set; }
        public int ActiveProperties { get; set; }
        public int PendingApplications { get; set; }

        public int TotalBookings { get; set; }
        public int BookingsThisMonth { get; set; }
        public int PendingRequests { get; set; }
        public int CheckInsToReview { get; set; }

        public decimal TotalEarnings { get; set; }
        public decimal EarningsThisMonth { get; set; }

        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }

        /// <summary>Approved bookings / all decided requests, as a percentage.</summary>
        public double AcceptanceRate { get; set; }
        public decimal AverageBookingValue { get; set; }
        public double OccupancyRateThisMonth { get; set; }

        public List<Booking> PendingBookingRequests { get; set; } = new();
        public List<Booking> UpcomingStays { get; set; } = new();
    }

    // My properties
    public class HostPropertyListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public PropertyType PropertyType { get; set; }
        public decimal PricePerNight { get; set; }
        public int MaxGuests { get; set; }
        public int Bedrooms { get; set; }
        public int Beds { get; set; }
        public int Bathrooms { get; set; }
        public bool IsActive { get; set; }
        public string? CoverImageUrl { get; set; }
        public int ImageCount { get; set; }
        public int BookingCount { get; set; }
        public int ReviewCount { get; set; }
        public double AverageRating { get; set; }

        /// <summary>Status of the latest application for this property (null = no application, e.g. seeded data).</summary>
        public ApplicationStatus? ApplicationStatus { get; set; }

        /// <summary>Only approved properties can be switched on/off by the Host.</summary>
        public bool CanToggle => ApplicationStatus is null or Enums.ApplicationStatus.Approved;
    }

    // Edit property
    public class PropertyEditViewModel
    {
        public int Id { get; set; }

        [Required, StringLength(100, MinimumLength = 5)]
        [Display(Name = "Listing title")]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(4000, MinimumLength = 30)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Property type")]
        public PropertyType PropertyType { get; set; }

        [Required, StringLength(250)]
        [Display(Name = "Street address")]
        public string Address { get; set; } = string.Empty;

        [Required, StringLength(100)]
        [Display(Name = "City / town")]
        public string City { get; set; } = string.Empty;

        [Range(1, 50)]
        [Display(Name = "Maximum guests")]
        public int MaxGuests { get; set; }

        [Range(0, 50)]
        public int Bedrooms { get; set; }

        [Range(1, 50)]
        public int Beds { get; set; }

        [Range(0, 50)]
        public int Bathrooms { get; set; }

        [Range(typeof(decimal), "50", "1000000", ErrorMessage = "The nightly price must be between R50 and R1 000 000.")]
        [Display(Name = "Price per night (R)")]
        public decimal PricePerNight { get; set; }

        [Range(typeof(decimal), "0", "100000")]
        [Display(Name = "Cleaning fee (R)")]
        public decimal CleaningFee { get; set; }

        public List<int> SelectedAmenityIds { get; set; } = new();

        /// <summary>Extra photos to add (optional when editing).</summary>
        [Display(Name = "Add photos")]
        public List<IFormFile>? NewImages { get; set; }

        // Display only
        [ValidateNever] public List<AmenityOption> AvailableAmenities { get; set; } = new();
        [ValidateNever] public List<PropertyImage> ExistingImages { get; set; } = new();
        [ValidateNever] public bool IsActive { get; set; }
        [ValidateNever] public int MaxImages { get; set; } = 10;
    }

    // Booking requests
    public class HostBookingsViewModel
    {
        /// <summary>pending | upcoming | checkedin | past | all</summary>
        public string Filter { get; set; } = "pending";
        public int? PropertyId { get; set; }

        public List<Booking> Bookings { get; set; } = new();
        public List<HostPropertyOption> Properties { get; set; } = new();

        public int PendingCount { get; set; }
        public int UpcomingCount { get; set; }
        public int CheckedInCount { get; set; }
    }

    public class HostPropertyOption
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
    }

    // Check-in process configuration
    public class CheckInProcessViewModel
    {
        public int PropertyId { get; set; }

        [ValidateNever]
        public string PropertyTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "Give the check-in process a title.")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        public List<CheckInStepInput> Steps { get; set; } = new();
    }

    public class CheckInStepInput
    {
        [Required(ErrorMessage = "Each step needs a title.")]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Instructions { get; set; }

        [Display(Name = "Guest must upload a document")]
        public bool RequiresDocument { get; set; }

        [StringLength(100)]
        [Display(Name = "Document required")]
        public string? DocumentType { get; set; }
    }

    // Check-ins (document verification)
    public class HostCheckInsViewModel
    {
        public List<GuestCheckIn> AwaitingReview { get; set; } = new();
        public List<GuestCheckIn> InProgress { get; set; } = new();
        public List<GuestCheckIn> Completed { get; set; } = new();
    }

    public class CheckInReviewViewModel
    {
        public GuestCheckIn CheckIn { get; set; } = null!;
        public List<CheckInStep> Steps { get; set; } = new();
        public bool AllDocumentsVerified { get; set; }
    }

    // Earnings + reviews
    public class HostEarningsViewModel
    {
        public decimal TotalEarnings { get; set; }
        public decimal EarningsThisMonth { get; set; }
        public decimal ExpectedUpcoming { get; set; }
        public List<MonthlyEarning> Monthly { get; set; } = new();
        public List<Booking> PaidBookings { get; set; } = new();
    }

    public class MonthlyEarning
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Amount { get; set; }
        public int Bookings { get; set; }
        public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy");
    }

    public class HostReviewsViewModel
    {
        public double AverageRating { get; set; }
        public List<Review> Reviews { get; set; } = new();
    }
}
