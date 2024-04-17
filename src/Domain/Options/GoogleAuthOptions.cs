namespace Domain.Options;

public class GoogleAuthOptions
{
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string Scope { get; set; } = default!;
    public string OauthUrl { get; set; } = default!;
    public string TokenUrl { get; set; } = default!;
    public string ProfileUrl { get; set; } = default!;
}