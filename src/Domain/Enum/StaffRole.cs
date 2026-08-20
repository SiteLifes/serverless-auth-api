namespace Domain.Enum;

/// <summary>
/// Roles for internal staff using the back office panel.
///
/// Two levels on purpose: finer grained roles were considered and dropped, because an enum value no
/// policy actually checks reads as a guarantee that is not there. Split further when a real need
/// appears, not in anticipation of one.
/// </summary>
public enum StaffRole
{
    /// <summary>Reads any site, changes nothing.</summary>
    ReadOnly = 1,

    /// <summary>Everything a site manager can do, on any site.</summary>
    Admin = 2
}
