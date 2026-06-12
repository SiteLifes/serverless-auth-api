using Domain.Options;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.Middleware;

public class ApiKeyValidatorMiddleware : IMiddleware
{
    private static readonly HashSet<string> ProtectedRegistrationPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/v1/login/apply/otp",
        // "/v1/register/validate-otp",
        "/v1/register",
        "/v1/anyregister"
    };

    private readonly IOptionsSnapshot<ApiKeyValidationSettings> _apiKeyValidationSettings;

    public ApiKeyValidatorMiddleware(IOptionsSnapshot<ApiKeyValidationSettings> apiKeyValidationSettings)
    {
        _apiKeyValidationSettings = apiKeyValidationSettings;
    }

    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var settings = _apiKeyValidationSettings.Value;
        var requestPath = context.Request.Path.ToString();
        var normalizedPath = requestPath.StartsWith('/') ? requestPath : $"/{requestPath}";

        var isHealthCheck = normalizedPath.Equals("/ping", StringComparison.OrdinalIgnoreCase) ||
                            normalizedPath.Equals("ping", StringComparison.OrdinalIgnoreCase);
        if (isHealthCheck)
            return next(context);

        var isProtectedRegistrationPath = ProtectedRegistrationPaths.Contains(normalizedPath);
        var shouldAlwaysProtectRegistrationPath = settings.AlwaysProtectRegistrationPaths && isProtectedRegistrationPath;
        var isWhitelisted = settings.WhiteList.Any(path =>
            normalizedPath.Equals(path, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.Equals(path.StartsWith('/') ? path : $"/{path}", StringComparison.OrdinalIgnoreCase));

        if (!shouldAlwaysProtectRegistrationPath && (!settings.IsEnabled || isWhitelisted))
            return next(context);

        var headerName = string.IsNullOrWhiteSpace(settings.HeaderName) ? "x-api-key" : settings.HeaderName;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) ||
            !context.Request.Headers.TryGetValue(headerName, out var apiKey) ||
            apiKey != settings.ApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        return next(context);
    }
}