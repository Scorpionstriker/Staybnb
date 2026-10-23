using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Staybnb.Data;
using Staybnb.Services.Interfaces;

namespace Staybnb.Controllers.Api
{
    /// <summary>
    /// Small JSON API (study guide chapter 6) used by the booking box on the property page:
    ///   GET /api/properties/5/quote?checkIn=2026-01-28&amp;checkOut=2026-01-30&amp;guests=2
    ///   GET /api/properties/5/booked-dates
    /// The same BookingService is used here and in the MVC controllers, so the price the
    /// guest sees while choosing dates is exactly what the server charges.
    /// </summary>
    [ApiController]
    [Route("api/properties")]
    [Produces("application/json")]
    public class PropertiesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IBookingService _bookings;

        public PropertiesApiController(ApplicationDbContext context, IBookingService bookings)
        {
            _context = context;
            _bookings = bookings;
        }

        // GET /api/properties/5/quote?checkIn=2026-01-28&checkOut=2026-01-30&guests=2
        [HttpGet("{id:int}/quote")]
        [ProducesResponseType(typeof(QuoteResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<QuoteResponse>> Quote(int id, [FromQuery] DateTime checkIn, [FromQuery] DateTime checkOut, [FromQuery] int guests = 1)
        {
            var property = await _context.HostProperties.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            if (property == null)
                return NotFound(new { message = "Property not found." });

            var price = _bookings.CalculatePrice(property, checkIn, checkOut);
            var response = new QuoteResponse
            {
                Nights = price.Nights,
                PricePerNight = price.PricePerNight,
                Subtotal = price.Subtotal,
                CleaningFee = price.CleaningFee,
                ServiceFee = price.ServiceFee,
                Total = price.Total
            };

            var validation = _bookings.ValidateRequest(property, checkIn, checkOut, guests);
            if (!validation.Succeeded)
            {
                response.Available = false;
                response.Message = validation.Message;
                return Ok(response);
            }

            if (!await _bookings.IsAvailableAsync(id, checkIn, checkOut))
            {
                response.Available = false;
                response.Message = "Those dates are already booked. Please choose different dates.";
                return Ok(response);
            }

            response.Available = true;
            return Ok(response);
        }

        // GET /api/properties/5/booked-dates
        [HttpGet("{id:int}/booked-dates")]
        public async Task<ActionResult<IEnumerable<object>>> BookedDates(int id)
        {
            var exists = await _context.HostProperties.AnyAsync(p => p.Id == id && p.IsActive);
            if (!exists) return NotFound();

            var ranges = await _bookings.GetBookedRangesAsync(id);
            return Ok(ranges.Select(r => new
            {
                checkIn = r.CheckIn.ToString("yyyy-MM-dd"),
                checkOut = r.CheckOut.ToString("yyyy-MM-dd")
            }));
        }

        public class QuoteResponse
        {
            public bool Available { get; set; }
            public string? Message { get; set; }
            public int Nights { get; set; }
            public decimal PricePerNight { get; set; }
            public decimal Subtotal { get; set; }
            public decimal CleaningFee { get; set; }
            public decimal ServiceFee { get; set; }
            public decimal Total { get; set; }
        }
    }
}
