namespace Staybnb.Models.Common
{
    /// <summary>
    /// Result of the booking price calculation:
    /// TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
    /// </summary>
    public class PriceBreakdown
    {
        public int Nights { get; set; }
        public decimal PricePerNight { get; set; }

        /// <summary>PricePerNight x Nights</summary>
        public decimal Subtotal { get; set; }

        public decimal CleaningFee { get; set; }
        public decimal ServiceFee { get; set; }

        /// <summary>The percentage used for the service fee, e.g. 0.12.</summary>
        public decimal ServiceFeePercentage { get; set; }

        public decimal Total { get; set; }
    }
}
