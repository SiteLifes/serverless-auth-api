using System.Text;
using Domain.Services;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Infrastructure.Services;

public class CryptoService : ICryptoService
{
    public CryptoService()
    {
        
    }
    public string HashPassword(string password)
    {
        var salt = Encoding.ASCII.GetBytes("");
        return Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: password!,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));
    }
}