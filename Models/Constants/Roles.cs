namespace Staybnb.Models.Constants
{
    /// <summary>
    /// Role names in one place, so a typo like "Hsot" can never slip into an [Authorize] attribute.
    /// Usage: [Authorize(Roles = Roles.Host)]  or  [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    /// </summary>
    public static class Roles
    {
        public const string Guest = "Guest";
        public const string Host = "Host";
        public const string Admin = "Admin";
        public const string SuperAdmin = "SuperAdmin";

        /// <summary>Comma-separated = "either role is allowed" in [Authorize(Roles = ...)].</summary>
        public const string AdminOrSuperAdmin = Admin + "," + SuperAdmin;

        public static readonly string[] All = { Guest, Host, Admin, SuperAdmin };
    }
}
