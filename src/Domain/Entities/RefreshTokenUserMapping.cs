using System.Text.Json.Serialization;
using Domain.Entities.Base;
using Domain.Extensions;

namespace Domain.Entities;

public class RefreshTokenUserMapping : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk(UserId);
    [JsonPropertyName("sk")] public string Sk => RefreshToken;

    [JsonPropertyName("userId")] public string UserId { get; set; } = default!;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = default!;

    [JsonPropertyName("expireAt")] public DateTime ExpireAt { get; set; }

    [JsonPropertyName("ttl")] public long Ttl => ExpireAt.ToUnixTimeSeconds();

    public static string GetPk(string? pkKey = null) => $"RefreshToken#{pkKey}";
}