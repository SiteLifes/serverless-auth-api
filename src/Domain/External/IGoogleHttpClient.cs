using Domain.Domains.Google.Auth;

namespace Domain.External;

public interface IGoogleHttpClient
{
    Task<GoogleTokenResponse?> GetTokenAsync(string code, CancellationToken cancellationToken = default);
    
    Task<GoogleProfileResponse?> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default);
}