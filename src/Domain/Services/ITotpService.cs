namespace Domain.Services;

public interface ITotpService
{
    /// <summary>Generates a new base32 shared secret for an authenticator app.</summary>
    string GenerateSecret();

    /// <summary>
    /// otpauth:// URI an authenticator app can consume, usually rendered as a QR code.
    /// </summary>
    string BuildProvisioningUri(string secret, string accountName, string issuer);

    /// <summary>
    /// Verifies a code against the secret, tolerating a small clock drift in both directions.
    /// </summary>
    bool VerifyCode(string secret, string code, DateTimeOffset? at = null);
}
