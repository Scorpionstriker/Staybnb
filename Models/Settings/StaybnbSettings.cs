namespace Staybnb.Models.Settings
{
    /// <summary>
    /// Values from the "Staybnb" section of appsettings.json.
    /// Registered in Program.cs with builder.Services.Configure&lt;StaybnbSettings&gt;(...)
    /// and injected into services as IOptions&lt;StaybnbSettings&gt;.
    /// </summary>
    public class StaybnbSettings
    {
        /// <summary>Service fee as a fraction of the nightly subtotal (0.12 = 12%).</summary>
        public decimal ServiceFeePercentage { get; set; } = 0.12m;

        public int MinNights { get; set; } = 1;

        public int MaxNights { get; set; } = 30;

        /// <summary>How far ahead (in days) a guest may book.</summary>
        public int MaxDaysInAdvance { get; set; } = 365;

        public int MaxImageSizeMb { get; set; } = 5;

        public int MaxDocumentSizeMb { get; set; } = 10;

        public int MaxImagesPerProperty { get; set; } = 10;
    }
}
