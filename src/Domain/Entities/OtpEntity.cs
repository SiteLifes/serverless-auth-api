using System.Text.Json.Serialization;
using Domain.Entities.Base;
using Domain.Extensions;

namespace Domain.Entities;

public class OtpEntity : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk(Key);

    [JsonPropertyName("sk")] public string Sk => Otp;
    [JsonPropertyName("phone")] public string Key { get; set; } = default!;

    [JsonPropertyName("userId")] public string? UserId { get; set; }
    [JsonPropertyName("otp")] public string Otp { get; set; } = default!;

    /// <summary>
    /// The staff account that had this code issued without delivering it, or null for the ordinary
    /// login codes residents request themselves. See the staff issue-login-otp endpoint.
    /// </summary>
    [JsonPropertyName("issuedByStaffId")] public string? IssuedByStaffId { get; set; }

    /// <summary>
    /// Expiry, in unix seconds. Settable rather than computed so it survives a round trip: Dynamo
    /// deletes expired items lazily, so anything that has to know whether a stored code is still
    /// live has to read the moment it was written for rather than recompute five minutes from now.
    /// </summary>
    [JsonPropertyName("ttl")] public long Ttl { get; set; } = DateTime.UtcNow.AddMinutes(5).ToUnixTimeSeconds();

    public static string GetPk(string pkKey)
    {
        return $"LoginOtp#{pkKey}";
    }
}