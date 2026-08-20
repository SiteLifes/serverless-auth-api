using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Domain.Services;

namespace Infrastructure.Services;

/// <summary>
/// RFC 6238 TOTP over RFC 4226 HOTP, using HMAC-SHA1, 6 digits and a 30 second step: the defaults
/// every authenticator app assumes.
///
/// Chosen over SMS or email codes for staff: no per-message cost, no delivery failures, works offline.
/// </summary>
public class TotpService : ITotpService
{
    private const int Digits = 6;
    private const int StepSeconds = 30;

    /// <summary>Number of steps accepted either side of now, to absorb clock drift.</summary>
    private const int AllowedDriftSteps = 1;

    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public string GenerateSecret()
    {
        // 20 bytes is the HMAC-SHA1 block-matched length recommended by RFC 4226.
        return ToBase32(RandomNumberGenerator.GetBytes(20));
    }

    public string BuildProvisioningUri(string secret, string accountName, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);

        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}" +
               $"?secret={secret}" +
               $"&issuer={encodedIssuer}" +
               $"&algorithm=SHA1" +
               $"&digits={Digits}" +
               $"&period={StepSeconds}";
    }

    public bool VerifyCode(string secret, string code, DateTimeOffset? at = null)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        code = code.Trim();
        if (code.Length != Digits || !code.All(char.IsDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = FromBase32(secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var currentStep = timestamp / StepSeconds;

        var matched = false;
        for (var offset = -AllowedDriftSteps; offset <= AllowedDriftSteps; offset++)
        {
            var candidate = ComputeCode(key, currentStep + offset);

            // Compare every candidate rather than breaking early, so verification takes the same
            // time whichever step matched.
            matched |= CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(candidate),
                Encoding.ASCII.GetBytes(code));
        }

        return matched;
    }

    internal static string ComputeCode(byte[] key, long step)
    {
        var counter = BitConverter.GetBytes(step);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counter);
        }

        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counter);

        // Dynamic truncation, RFC 4226 section 5.3.
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        var otp = binary % (int) Math.Pow(10, Digits);
        return otp.ToString(CultureInfo.InvariantCulture).PadLeft(Digits, '0');
    }

    private static string ToBase32(byte[] data)
    {
        var builder = new StringBuilder();
        int buffer = 0, bitsLeft = 0;

        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                builder.Append(Base32Alphabet[(buffer >> (bitsLeft - 5)) & 31]);
                bitsLeft -= 5;
            }
        }

        if (bitsLeft > 0)
        {
            builder.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 31]);
        }

        return builder.ToString();
    }

    private static byte[] FromBase32(string value)
    {
        value = value.TrimEnd('=').Replace(" ", string.Empty).ToUpperInvariant();

        var output = new List<byte>(value.Length * 5 / 8);
        int buffer = 0, bitsLeft = 0;

        foreach (var c in value)
        {
            var index = Base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException($"Invalid base32 character '{c}'.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                output.Add((byte) ((buffer >> (bitsLeft - 8)) & 0xFF));
                bitsLeft -= 8;
            }
        }

        return output.ToArray();
    }
}
