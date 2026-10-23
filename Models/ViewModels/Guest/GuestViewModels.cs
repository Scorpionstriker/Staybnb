using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Staybnb.Models.Common;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;

namespace Staybnb.Models.ViewModels.Guest
{
    // =====================================================================
    // PROPERTY GALLERY (search + filters)
    // =====================================================================
    public class PropertySearchViewModel
    {
        // ---------- Filters (bound from the query string) ----------
        public string? City { get; set; }
        public PropertyType? Type { get; set; }
        public int? Guests { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CheckIn { get; set; }

        [DataType(DataType.Date)]
        public DateTime? CheckOut { get; set; }

        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }

        /// <summary>recommended | price_asc | price_desc | rating | newest</summary>
        public string Sort { get; set; } = "recommended";

        public int Page { get; set; } = 1;

        // ---------- Results ----------
        public List<PropertyCardViewModel> Results { get; set; } = new();
        public int TotalResults { get; set; }
        public int TotalPages { get; set; }
        public List<string> Cities { get; set; } = new();

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(City) || Type.HasValue || Guests.HasValue ||
            CheckIn.HasValue || CheckOut.HasValue || MinPrice.HasValue || MaxPrice.HasValue;
    }

    public class PropertyCardViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public PropertyType PropertyType { get; set; }
        public decimal PricePerNight { get; set; }
        public decimal CleaningFee { get; set; }
        public int MaxGuests { get; set; }
        public int Bedrooms { get; set; }
        public int Beds { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsWishlisted { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Total for the searched dates (null when no dates were searched).</summary>
        public decimal? StayTotal { get; set; }
        public int? StayNights { get; set; }
    }

    // =====================================================================
    // PROPERTY DETAILS (/Properties/View/5)
    // =====================================================================
    public class PropertyDetailsViewModel
    {
        public HostProperty Property { get; set; } = null!;
        public List<PropertyImage> Images { get; set; } = new();
        public List<Amenity> Amenities { get; set; } = new();
        public List<Review> Reviews { get; set; } = new();
        public double AverageRating { get; set; }

        public ApplicationUser Host { get; set; } = null!;
        public int HostListingCount { get; set; }
        public int HostReviewCount { get; set; }

        public List<Staybnb.Services.Interfaces.BookedRange> BookedRanges { get; set; } = new();

        public bool IsWishlisted { get; set; }
        public bool IsGuest { get; set; }
        public bool IsOwner { get; set; }
        public bool IsAuthenticated { get; set; }

        public decimal ServiceFeePercentage { get; set; }

        // Pre-filled booking box (from the search, or sensible defaults)
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public int Guests { get; set; } = 1;
        public PriceBreakdown InitialPrice { get; set; } = new();
    }

    // =====================================================================
    // BOOKING: confirm ("Request to book") + create
    // =====================================================================
    public class BookingConfirmViewModel
    {
        [Required]
        public int PropertyId { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Check-in")]
        public DateTime CheckIn { get; set; }

        [Required, DataType(DataType.Date)]
        [Display(Name = "Check-out")]
        public DateTime CheckOut { get; set; }

        [Range(1, 50)]
        public int Guests { get; set; } = 1;

        [StringLength(500)]
        [Display(Name = "Message to the Host (optional)")]
        public string? GuestMessage { get; set; }

        // ---------- Display only ----------
        [ValidateNever] public HostProperty? Property { get; set; }
        [ValidateNever] public string? CoverImageUrl { get; set; }
        [ValidateNever] public PriceBreakdown? Price { get; set; }
        [ValidateNever] public string? Problem { get; set; }
    }

    // =====================================================================
    // MY TRIPS + BOOKING DETAILS
    // =====================================================================
    public class MyTripsViewModel
    {
        /// <summary>upcoming | pending | past | cancelled</summary>
        public string Filter { get; set; } = "upcoming";
        public List<Booking> Bookings { get; set; } = new();
        public int UpcomingCount { get; set; }
        public int PendingCount { get; set; }
    }

    public class BookingDetailsViewModel
    {
        public Booking Booking { get; set; } = null!;
        public string? CoverImageUrl { get; set; }
        public bool CanCancel { get; set; }
        public bool CanCheckIn { get; set; }
        public bool CanReview { get; set; }
        public Review? ExistingReview { get; set; }
    }

    public class ReviewInputModel
    {
        public int BookingId { get; set; }

        [Range(1, 5, ErrorMessage = "Choose a rating from 1 to 5 stars.")]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }
    }

    // =====================================================================
    // CHECK-IN WIZARD
    // =====================================================================
    public class CheckInWizardViewModel
    {
        public GuestCheckIn CheckIn { get; set; } = null!;
        public List<CheckInStep> Steps { get; set; } = new();
        public CheckInStep? CurrentStep { get; set; }

        /// <summary>The document already uploaded for the current step (if any).</summary>
        public GuestDocument? CurrentDocument { get; set; }

        public bool IsSubmitted => CheckIn.Status is CheckInStatus.Submitted or CheckInStatus.Verified;
        public int TotalSteps => Steps.Count;
        public int ProgressPercent => TotalSteps == 0 ? 0 :
            IsSubmitted ? 100 : (int)Math.Round(100.0 * (CheckIn.CurrentStep - 1) / TotalSteps);
    }

    // =====================================================================
    // WISHLIST
    // =====================================================================
    public class WishlistViewModel
    {
        public List<PropertyCardViewModel> Properties { get; set; } = new();
    }
}
