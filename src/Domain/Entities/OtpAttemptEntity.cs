using System.Text.Json.Serialization;
using Domain.Entities.Base;
using Domain.Extensions;

namespace Domain.Entities;

public class OtpAttemptEntity : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk(Key);
    [JsonPropertyName("sk")] public string Sk => GetSk();

    [JsonPropertyName("key")] public string Key { get; set; } = default!;
    [JsonPropertyName("failedAttempts")] public int FailedAttempts { get; set; }
    [JsonPropertyName("lockedUntilUtc")] public DateTime? LockedUntilUtc { get; set; }
    [JsonPropertyName("ttl")] public long Ttl => DateTime.UtcNow.AddDays(1).ToUnixTimeSeconds();

    public static string GetPk(string key)
    {
        return $"OtpAttempt#{key}";
    }

    public static string GetSk()
    {
        return "Status";
    }
}

