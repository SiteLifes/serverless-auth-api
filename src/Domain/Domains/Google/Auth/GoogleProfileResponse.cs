using System.Text.Json.Serialization;

namespace Domain.Domains.Google.Auth;

public class GoogleProfileResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = default!;
    [JsonPropertyName("email")] public string Email { get; set; } = default!;
    [JsonPropertyName("verified_email")] public bool VerifiedEmail { get; set; }
    [JsonPropertyName("picture")] public string Picture { get; set; } = default!;
}