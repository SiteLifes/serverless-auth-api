using System.Text.Json;
using Domain.Domains.Google.Auth;
using Domain.External;
using Domain.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.External;

public class GoogleHttpClient : IGoogleHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsSnapshot<GoogleAuthOptions> _googleAuthOptions;

    public GoogleHttpClient(HttpClient httpClient, IOptionsSnapshot<GoogleAuthOptions> googleAuthOptions)
    {
        _httpClient = httpClient;
        _googleAuthOptions = googleAuthOptions;
    }

    public async Task<GoogleTokenResponse?> GetTokenAsync(string code, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(_googleAuthOptions.Value.TokenUrl, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", _googleAuthOptions.Value.ClientId },
                { "client_secret", _googleAuthOptions.Value.ClientSecret },
                { "redirect_uri", _googleAuthOptions.Value.RedirectUri },
                { "grant_type", "authorization_code" }
            }), cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<GoogleTokenResponse>(content);
    }

    public async Task<GoogleProfileResponse?> GetProfileAsync(string accessToken,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"{_googleAuthOptions.Value.ProfileUrl}?access_token={accessToken}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<GoogleProfileResponse>(content);
    }
}