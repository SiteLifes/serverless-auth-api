namespace Domain.Options;

public class StaffAuthOptions
{
    public const string SectionName = "StaffAuth";

    /// <summary>
    /// Staff login is off unless explicitly enabled. These accounts can act on every site, so the
    /// endpoints stay unreachable until someone turns them on deliberately.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Staff tokens live shorter than resident tokens.</summary>
    public int ExpireMinutes { get; set; } = 60;

    /// <summary>
    /// Whether a TOTP code is required at login. On by default; turning it off is a deliberate
    /// downgrade for accounts that can act on every site.
    /// </summary>
    public bool RequireTwoFactor { get; set; } = true;

    /// <summary>Name shown in the authenticator app.</summary>
    public string TwoFactorIssuer { get; set; } = "SiteLifes";
}
