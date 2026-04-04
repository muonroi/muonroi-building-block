namespace Muonroi.Core.Abstractions.Constants;

/// <summary>
/// Defines static constants for common role and user names used in the application.
/// </summary>
public static class StaticRoleAndUserNames
{
    /// <summary>
    /// Contains static constants for host-level roles and user names.
    /// </summary>
    public static class Host
    {
        /// <summary>
        /// The name of the host administrator role.
        /// </summary>
        public const string Admin = "Admin";

        /// <summary>
        /// The name of the host user role.
        /// </summary>
        public const string User = "User";

        /// <summary>
        /// The default user name for the host administrator.
        /// </summary>
        public const string AdminUserName = "admin";
    }
}
