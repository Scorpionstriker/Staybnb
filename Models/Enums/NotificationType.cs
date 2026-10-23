namespace Staybnb.Models.Enums
{
    /// <summary>
    /// Type of notification shown in the bell-icon notification centre.
    /// </summary>
    public enum NotificationType
    {
        General,
        ApplicationSubmitted,
        ApplicationApproved,
        ApplicationRejected,
        BookingRequested,
        BookingApproved,
        BookingRejected,
        BookingCancelled,
        CheckInSubmitted,
        DocumentVerified,
        DocumentRejected,
        NewMessage,
        RoleChanged
    }
}
