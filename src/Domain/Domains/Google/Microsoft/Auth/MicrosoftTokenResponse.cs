using System.Text.Json.Serialization;

namespace Domain.Domains.Google.Microsoft.Auth;

public class MicrosoftTokenResponse
{
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = default!;

    [JsonPropertyName("scope")] public string Scope { get; set; } = default!;

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }

    [JsonPropertyName("ext_expires_in")] public int ExtExpiresIn { get; set; }

    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = default!;

    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = default!;

    [JsonPropertyName("id_token")] public string IdToken { get; set; } = default!;
}