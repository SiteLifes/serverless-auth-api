namespace Domain.Enum;

/// <summary>
/// Roles for internal staff using the back office panel.
///
/// Each level strictly contains the one below it, so a policy checks "at least this" rather than a
/// set. Split further when a real need appears, not in anticipation of one: an enum value no policy
/// actually checks reads as a guarantee that is not there.
/// </summary>
public enum StaffRole
{
    /// <summary>Reads any site, changes nothing.</summary>
    ReadOnly = 1,

    /// <summary>Everything a site manager can do, on any site.</summary>
    Admin = 2,

    /// <summary>
    /// Also manages the panel's own accounts: who has one, at what level, and resetting their
    /// passwords. Kept apart from Admin because that is the power to hand out access, which is a
    /// different thing from using it.
    /// </summary>
    SuperAdmin = 3
}
