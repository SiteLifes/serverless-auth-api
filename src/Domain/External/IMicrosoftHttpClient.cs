using Domain.Domains.Google.Microsoft.Auth;

namespace Domain.External;

public interface IMicrosoftHttpClient
{
    Task<MicrosoftTokenResponse?> GetTokenAsync(string code, CancellationToken cancellationToken = default);
    
    Task<MicrosoftProfileResponse?> GetProfileAsync(string accessToken, CancellationToken cancellationToken = default);
}