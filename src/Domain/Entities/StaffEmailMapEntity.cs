using System.Text.Json.Serialization;
using Domain.Entities.Base;

namespace Domain.Entities;

/// <summary>
/// Email to staff id lookup, so login is a single partition query instead of a scan.
/// </summary>
public class StaffEmailMapEntity : IEntity
{
    [JsonPropertyName("pk")] public string Pk => GetPk(Email);

    [JsonPropertyName("sk")] public string Sk => StaffId;

    [JsonPropertyName("email")] public string Email { get; set; } = default!;

    [JsonPropertyName("staffId")] public string StaffId { get; set; } = default!;

    public static string GetPk(string email) => $"staffEmail#{email.ToLowerInvariant()}";
}
