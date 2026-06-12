namespace Domain.Options;

public class ApiKeyValidationSettings
{
    public bool IsEnabled { get; set; }
    public bool AlwaysProtectRegistrationPaths { get; set; } = true;
    public string? ApiKey { get; set; }

    public string? HeaderName { get; set; }
    public List<string> WhiteList { get; set; } = new();
}