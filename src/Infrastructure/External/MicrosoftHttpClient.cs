using System.Net.Http.Headers;
using System.Text.Json;
using Domain.Domains.Google.Microsoft.Auth;
using Domain.External;
using Domain.Options;
using Microsoft.Extensions.Options;

namespace Infrastructure.External;

public class MicrosoftHttpClient : IMicrosoftHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsSnapshot<MicrosoftAuthOptions> _microsoftAuthOptions;

    public MicrosoftHttpClient(HttpClient httpClient,
        IOptionsSnapshot<MicrosoftAuthOptions> microsoftAuthOptions)
    {
        _httpClient = httpClient;
        _microsoftAuthOptions = microsoftAuthOptions;
    }

    public async Task<MicrosoftTokenResponse?> GetTokenAsync(string code, CancellationToken cancellationToken = default)
    {
        // var tokenRequestContent = new StringContent(
        //     $"client_id={_microsoftAuthOptions.Value.ClientId}&client_secret={_microsoftAuthOptions.Value.ClientSecret}&code={code}&redirect_uri={_microsoftAuthOptions.Value.RedirectUri}&grant_type=authorization_code",
        //     Encoding.UTF8, "application/x-www-form-urlencoded");
        var url = _microsoftAuthOptions.Value.TokenUrl.Replace("{tenantId}", _microsoftAuthOptions.Value.TenantId);
        var tokenResponse = await _httpClient.PostAsync(url, new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "code", code },
                { "client_id", _microsoftAuthOptions.Value.ClientId },
                { "client_secret", _microsoftAuthOptions.Value.ClientSecret },
                { "redirect_uri", _microsoftAuthOptions.Value.RedirectUri },
                { "grant_type", "authorization_code" }
            }), cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
            return null;
        var tokenResponseContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<MicrosoftTokenResponse>(tokenResponseContent);
    }

    public async Task<MicrosoftProfileResponse?> GetProfileAsync(string accessToken,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage();
        request.Method = HttpMethod.Get;
        request.RequestUri = new Uri(_microsoftAuthOptions.Value.ProfileUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var profileResponse = await _httpClient.SendAsync(request, cancellationToken);

        if (!profileResponse.IsSuccessStatusCode)
            return null;
        var profileResponseContent = await profileResponse.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<MicrosoftProfileResponse>(profileResponseContent);
    }
}