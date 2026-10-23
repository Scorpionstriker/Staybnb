using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Models.Entities;
using Staybnb.Models.Enums;
using Staybnb.Models.ViewModels.Guest;

namespace Staybnb.Services
{
    /// <summary>
    /// Re-usable LINQ pieces for listing properties as gallery cards
    /// (used by the gallery and the wishlist).
    /// </summary>
    public static class PropertyQueries
    {
        /// <summary>Converts properties into gallery cards in a single SQL query.</summary>
        public static IQueryable<PropertyCardViewModel> ToCards(this IQueryable<HostProperty> query) =>
            query.Select(p => new PropertyCardViewModel
            {
                Id = p.Id,
                Title = p.Title,
                City = p.City,
                PropertyType = p.PropertyType,
                PricePerNight = p.PricePerNight,
                CleaningFee = p.CleaningFee,
                MaxGuests = p.MaxGuests,
                Bedrooms = p.Bedrooms,
                Beds = p.Beds,
                CreatedAt = p.CreatedAt,
                ImageUrls = p.Images
                    .OrderByDescending(i => i.IsCover).ThenBy(i => i.Id)
                    .Select(i => i.ImageUrl)
                    .Take(5)
                    .ToList(),
                ReviewCount = p.Reviews.Count,
                AverageRating = p.Reviews.Any() ? p.Reviews.Average(r => (double)r.Rating) : 0
            });

        /// <summary>Removes properties that already have an Approved / CheckedIn stay overlapping the dates.</summary>
        public static IQueryable<HostProperty> AvailableBetween(this IQueryable<HostProperty> query, DateTime checkIn, DateTime checkOut)
        {
            var from = checkIn.Date;
            var to = checkOut.Date;
            return query.Where(p => !p.Bookings.Any(b =>
                (b.Status == BookingStatus.Approved || b.Status == BookingStatus.CheckedIn)
                && b.CheckInDate < to
                && b.CheckOutDate > from));
        }

        /// <summary>Ids of the properties this user has saved (empty for anonymous users).</summary>
        public static async Task<HashSet<int>> GetWishlistIdsAsync(this ApplicationDbContext context, string? userId)
        {
            if (string.IsNullOrEmpty(userId)) return new HashSet<int>();

            var ids = await context.WishlistItems
                .Where(w => w.UserId == userId)
                .Select(w => w.PropertyId)
                .ToListAsync();

            return ids.ToHashSet();
        }
    }
}
