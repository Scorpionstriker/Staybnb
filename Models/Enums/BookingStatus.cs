namespace Staybnb.Models.Enums
{
    /// <summary>
    /// Lifecycle of a booking: Pending -> Approved -> CheckedIn -> Completed.
    /// A booking can also end as Rejected (by the Host) or Cancelled (by the Guest).
    /// </summary>
    public enum BookingStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        CheckedIn,
        Completed
    }
}
