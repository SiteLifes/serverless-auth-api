namespace Domain.Enum;

/// <summary>
/// Roles for internal staff using the back office panel.
///
/// ReadOnly, Admin and SuperAdmin nest: each strictly contains the one below it, so a policy checks
/// "at least this" rather than a set. FieldSales sits beside Admin rather than above or below it:
/// the same powers, confined to the customers assigned to that person. Split further when a real
/// need appears, not in anticipation of one: an enum value no policy actually checks reads as a
/// guarantee that is not there.
///
/// The panel calls Admin "Tele Satış" and FieldSales "Saha Satış"; the wire names stay as they are
/// because the gateway (StaffClaims) mirrors them.
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
    SuperAdmin = 3,

    /// <summary>
    /// Saha Satış: everything Admin can do, but only on their own customers — potential sites,
    /// contact and referral requests, WhatsApp conversations and sites assigned to them (or opened
    /// by them). The gateway enforces the site half and tells Property to narrow the lists.
    /// </summary>
    FieldSales = 4
}
