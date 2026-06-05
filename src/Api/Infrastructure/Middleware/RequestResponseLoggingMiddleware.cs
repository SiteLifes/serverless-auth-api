using System.Diagnostics;
using System.Text;

namespace Api.Infrastructure.Middleware;

public sealed class RequestResponseLoggingMiddleware : IMiddleware
{
    private const int BodyLogLimit = 4096;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var stopwatch = Stopwatch.StartNew();

        var requestBody = await ReadRequestBodyAsync(context.Request);
        _logger.LogInformation(
            "HTTP Request {Method} {Path}{QueryString} Headers={Headers} Body={Body}",
            context.Request.Method,
            context.Request.Path,
            context.Request.QueryString,
            ToSafeDictionary(context.Request.Headers),
            requestBody);

        var originalBodyStream = context.Response.Body;
        await using var responseBodyStream = new MemoryStream();
        context.Response.Body = responseBodyStream;

        try
        {
            await next(context);

            var responseBody = await ReadResponseBodyAsync(context.Response);
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP Response {Method} {Path} Status={StatusCode} ElapsedMs={ElapsedMs} Headers={Headers} Body={Body}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                ToSafeDictionary(context.Response.Headers),
                responseBody);

            responseBodyStream.Position = 0;
            await responseBodyStream.CopyToAsync(originalBodyStream);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
        {
            return "<empty>";
        }

        if (!IsTextBasedContentType(request.ContentType))
        {
            return "<non-text body omitted>";
        }

        request.EnableBuffering();
        request.Body.Position = 0;

        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        request.Body.Position = 0;
        return Truncate(body);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        if (response.Body.Length == 0)
        {
            return "<empty>";
        }

        if (!IsTextBasedContentType(response.ContentType))
        {
            return "<non-text body omitted>";
        }

        response.Body.Position = 0;

        using var reader = new StreamReader(response.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();

        response.Body.Position = 0;
        return Truncate(body);
    }

    private static Dictionary<string, string> ToSafeDictionary(IHeaderDictionary headers)
    {
        return headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => Truncate(h.Value.ToString()));
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        return headerName.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
               headerName.Equals("x-api-key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTextBasedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return true;
        }

        return contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("text/", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/xml", StringComparison.OrdinalIgnoreCase) ||
               contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
    }

    private static string Truncate(string value)
    {
        return value.Length <= BodyLogLimit
            ? value
            : value[..BodyLogLimit] + "...(truncated)";
    }
}

