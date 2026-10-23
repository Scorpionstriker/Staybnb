namespace Staybnb.Models.Enums
{
    /// <summary>
    /// Category of an ActivityLog entry, used to filter the Admin "System Audit" page.
    /// </summary>
    public enum ActivityType
    {
        Login,
        Logout,
        FailedLogin,
        Registration,
        RoleChange,
        HostApplication,
        PropertyUpdate,
        BookingStatusChange,
        CheckIn,
        DocumentVerification,
        Messaging,
        System
    }
}
