using System.Security.Cryptography;
using Domain.Services;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Infrastructure.Services;

/// <summary>
/// PBKDF2 with a per-account random salt.
///
/// Deliberately separate from <see cref="CryptoService"/>, which hashes resident passwords with an
/// empty, shared salt: identical passwords produce identical hashes there. Staff accounts can act on
/// every site, so they do not inherit that. Resident hashing is left untouched because changing it
/// would invalidate every existing password.
/// </summary>
public class StaffPasswordHasher : IStaffPasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 210_000;

    public (string Hash, string Salt) HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public bool Verify(string password, string hash, string salt)
    {
        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
        {
            return false;
        }

        byte[] expected;
        byte[] saltBytes;
        try
        {
            expected = Convert.FromBase64String(hash);
            saltBytes = Convert.FromBase64String(salt);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, saltBytes);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt)
    {
        return KeyDerivation.Pbkdf2(
            password: password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: Iterations,
            numBytesRequested: HashBytes);
    }
}
