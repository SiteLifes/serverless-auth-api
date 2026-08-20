namespace Domain.Services;

public interface IStaffPasswordHasher
{
    /// <summary>Produces a fresh salt and the hash of <paramref name="password"/> under it.</summary>
    (string Hash, string Salt) HashPassword(string password);

    /// <summary>Constant-time verification of a candidate password.</summary>
    bool Verify(string password, string hash, string salt);
}
