using System.Text.Json.Serialization;

namespace Domain.Domains.Google.Auth;

public class GoogleTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = default!;
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("scope")] public string Scope { get; set; } = default!;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = default!;
    [JsonPropertyName("id_token")] public string IdToken { get; set; } = default!;
}