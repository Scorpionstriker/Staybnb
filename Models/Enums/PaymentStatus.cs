namespace Staybnb.Models.Enums
{
    /// <summary>
    /// Status of the (simulated) payment linked to a booking.
    /// Pending   - booking requested, waiting for the Host
    /// Paid      - Host approved the booking
    /// Refunded  - a paid booking was cancelled
    /// Cancelled - booking rejected/cancelled before it was paid
    /// </summary>
    public enum PaymentStatus
    {
        Pending,
        Paid,
        Refunded,
        Failed,
        Cancelled
    }
}
