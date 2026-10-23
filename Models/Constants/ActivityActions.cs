namespace Staybnb.Models.Constants
{
    /// <summary>
    /// Standard names for ActivityLog.Action, so the Admin audit page shows consistent values.
    /// </summary>
    public static class ActivityActions
    {
        // Account
        public const string UserRegistered = "UserRegistered";
        public const string UserLoggedIn = "UserLoggedIn";
        public const string UserLoggedOut = "UserLoggedOut";
        public const string LoginFailed = "LoginFailed";
        public const string AccountLockedOut = "AccountLockedOut";

        // Roles
        public const string RoleAssigned = "RoleAssigned";
        public const string RoleRemoved = "RoleRemoved";
        public const string PromotedToAdmin = "PromotedToAdmin";
        public const string GuestBecameHost = "GuestBecameHost";

        // Host applications
        public const string HostApplicationSubmitted = "HostApplicationSubmitted";
        public const string AdditionalPropertyApplicationSubmitted = "AdditionalPropertyApplicationSubmitted";
        public const string HostApplicationApproved = "HostApplicationApproved";
        public const string HostApplicationRejected = "HostApplicationRejected";

        // Properties
        public const string PropertyUpdated = "PropertyUpdated";
        public const string PropertyActivated = "PropertyActivated";
        public const string PropertyDeactivated = "PropertyDeactivated";
        public const string CheckInProcessUpdated = "CheckInProcessUpdated";

        // Bookings
        public const string BookingRequested = "BookingRequested";
        public const string BookingApproved = "BookingApproved";
        public const string BookingRejected = "BookingRejected";
        public const string BookingCancelled = "BookingCancelled";
        public const string BookingCheckedIn = "BookingCheckedIn";
        public const string BookingCompleted = "BookingCompleted";
        public const string PaymentCompleted = "PaymentCompleted";

        // Check-in
        public const string CheckInSubmitted = "CheckInSubmitted";
        public const string DocumentUploaded = "DocumentUploaded";
        public const string DocumentVerified = "DocumentVerified";
        public const string DocumentRejected = "DocumentRejected";

        // System
        public const string SystemSeeded = "SystemSeeded";
    }
}
