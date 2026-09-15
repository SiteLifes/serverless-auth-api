using System.Text.Json.Serialization;
using Domain.Entities.Base;
using Domain.Extensions;

namespace Domain.Entities;

public class RefreshTokenEntity : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk();
    [JsonPropertyName("sk")] public string Sk => RefreshToken;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = default!;

    [JsonPropertyName("userId")] public string UserId { get; set; } = default!;
    [JsonPropertyName("expireAt")] public DateTime ExpireAt { get; set; }

    // The token this one was issued in exchange for. It stays valid until this one is first used,
    // so a refresh response the client never received does not cost it the session.
    [JsonPropertyName("replacesRefreshToken")] public string? ReplacesRefreshToken { get; set; }

    [JsonPropertyName("ttl")] public long Ttl => ExpireAt.ToUnixTimeSeconds();

    public static string GetPk() => $"RefreshToken";
}