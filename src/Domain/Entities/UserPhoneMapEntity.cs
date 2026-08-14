using System.Text.Json.Serialization;
using Domain.Entities.Base;

namespace Domain.Entities;

public class UserPhoneMapEntity : IEntity
{
    public const string CanonicalSortKey = "!mapping";

    [JsonPropertyName("pk")] public string Pk => GetPk(Phone);

    // Legacy records use userId as the sort key. New records use a single,
    // deterministic key so DynamoDB can enforce one owner per phone number.
    [JsonPropertyName("sk")] public string Sk { get; set; } = CanonicalSortKey;
    [JsonPropertyName("phone")] public string Phone { get; set; } = default!;
    [JsonPropertyName("userId")] public string UserId { get; set; } = default!;

    public static string GetPk(string pkKey)
    {
        return $"UserMapping#{pkKey}";
    }
}
