namespace Domain.Constants;

/// <summary>
/// Claim names and values shared with the gateway. The gateway branches on these, so any change here
/// has to land there too.
/// </summary>
public static class AuthClaims
{
    public const string UserId = "userId";

    /// <summary>Distinguishes an end user of a site from internal staff.</summary>
    public const string UserType = "userType";

    /// <summary>Staff role. Emitted once per role the account holds.</summary>
    public const string Role = "roles";

    /// <summary>Display name, so the panel can show who is acting without another lookup.</summary>
    public const string FullName = "fullName";

    public static class UserTypes
    {
        /// <summary>A resident or site manager, i.e. every token issued today.</summary>
        public const string Resident = "Resident";

        /// <summary>Internal staff using the back office panel.</summary>
        public const string Staff = "Staff";
    }
}
