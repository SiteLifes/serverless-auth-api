namespace Domain.Services;

/// <summary>
/// What a staff password has to satisfy, in one place: it is enforced when an account is created,
/// when its owner changes it, and when a super admin resets it. Three copies of the same number
/// drift, and a rule that is looser in one path than the others is the same as not having it.
/// </summary>
public static class StaffPasswordPolicy
{
    public const int MinimumLength = 8;

    public const string TooShortMessage =
        "Staff passwords must be at least 8 characters.";
}
