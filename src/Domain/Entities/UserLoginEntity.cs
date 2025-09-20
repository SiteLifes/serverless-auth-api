using System.Text.Json.Serialization;
using Domain.Entities.Base;

namespace Domain.Entities;

public class UserLoginEntity : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk(UserId);

    [JsonPropertyName("sk")] public string Sk => Id;
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString();
    [JsonPropertyName("userId")] public string UserId { get; set; } = default!;
    [JsonPropertyName("createAt")] public DateTime CreateAt { get; set; } = DateTime.UtcNow;

    public static string GetPk(string pkKey)
    {
        return $"UserLogin#{pkKey}";
    }
}