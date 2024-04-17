using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.External;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.External.Microsoft;

public class Post : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] MicrosoftTokenRequest request,
        [FromServices] IMicrosoftHttpClient microsoftHttpClient,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        CancellationToken cancellationToken)
    {
        var tokenResponse = await microsoftHttpClient.GetTokenAsync(request.Code, cancellationToken);
        if (tokenResponse == null)
            return Results.Unauthorized();

        var profileResponse = await microsoftHttpClient.GetProfileAsync(tokenResponse.AccessToken, cancellationToken);
        if (profileResponse == null)
            return Results.Unauthorized();

        var emailAddress = profileResponse.Mail ?? profileResponse.UserPrincipalName;
        if (string.IsNullOrEmpty(emailAddress))
            return Results.Unauthorized();

        var userId = await authService.FindUserByEmail(emailAddress.ToLower(), cancellationToken);
        if (!string.IsNullOrEmpty(userId))
        {
            var token = await jwtService.CreateJwtAsync(userId, cancellationToken);
            return Results.Ok(token);
        }
        
        return Results.Forbid();
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/auth/external/microsoft/validate", Handler)
            .Produces200()
            .Produces400()
            .WithTags("External");
    }

    public record MicrosoftTokenRequest(string Code);
}