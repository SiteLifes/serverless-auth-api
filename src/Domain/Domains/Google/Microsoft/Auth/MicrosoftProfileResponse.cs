using System.Text.Json.Serialization;

namespace Domain.Domains.Google.Microsoft.Auth;

public class MicrosoftProfileResponse
{
    [JsonPropertyName("@odata.context")] public string Context { get; set; } = default!;

    [JsonPropertyName("businessPhones")] public string[] BusinessPhones { get; set; } = default!;

    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = default!;

    [JsonPropertyName("givenName")] public string GivenName { get; set; } = default!;

    [JsonPropertyName("jobTitle")] public string? JobTitle { get; set; }

    [JsonPropertyName("mail")] public string? Mail { get; set; }

    [JsonPropertyName("mobilePhone")] public string? MobilePhone { get; set; }

    [JsonPropertyName("officeLocation")] public string? OfficeLocation { get; set; }

    [JsonPropertyName("preferredLanguage")]
    public string? PreferredLanguage { get; set; }

    [JsonPropertyName("surname")] public string Surname { get; set; } = default!;

    [JsonPropertyName("userPrincipalName")]
    public string UserPrincipalName { get; set; } = default!;

    [JsonPropertyName("id")] public string Id { get; set; } = default!;
}