using Domain.Enum;

namespace Domain.Services;

/// <summary>
/// The rules for changing an existing staff account's roles, kept out of the endpoint so they can be
/// tested without a request pipeline.
///
/// A super admin may not change their own roles. That is what keeps the panel from losing its last
/// super admin: only a super admin can reach this, and whoever it demotes, the caller is still one.
/// </summary>
public static class StaffRoleChange
{
    public enum Refusal
    {
        None,
        OwnAccount,
        NoRole,
        UnknownRole
    }

    public static Refusal Check(string callerId, string targetId, IReadOnlyCollection<StaffRole>? requested)
    {
        if (string.Equals(callerId, targetId, StringComparison.Ordinal))
            return Refusal.OwnAccount;

        if (requested is null || requested.Count == 0)
            return Refusal.NoRole;

        // The wire format is the number, so a stray 7 deserialises happily into a StaffRole that no
        // policy has heard of.
        if (requested.Any(role => !System.Enum.IsDefined(role)))
            return Refusal.UnknownRole;

        return Refusal.None;
    }

    public static List<StaffRole> Normalize(IEnumerable<StaffRole> roles) =>
        roles.Distinct().OrderBy(role => role).ToList();

    public static bool IsUnchanged(IEnumerable<StaffRole> current, IEnumerable<StaffRole> requested) =>
        Normalize(current).SequenceEqual(Normalize(requested));
}
