using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.External;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.External.Google;

public class Post : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] GoogleTokenRequest request,
        [FromServices] IGoogleHttpClient googleHttpClient,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        CancellationToken cancellationToken)
    {
        var tokenResponse = await googleHttpClient.GetTokenAsync(request.Code, cancellationToken);
        if (tokenResponse == null)
            return Results.Unauthorized();

        var profileResponse = await googleHttpClient.GetProfileAsync(tokenResponse.AccessToken, cancellationToken);
        if (profileResponse == null)
            return Results.Unauthorized();


        var emailAddress = profileResponse.Email;
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
        return endpoints.MapPost("v1/auth/external/google/validate", Handler)
            .Produces200()
            .Produces400()
            .Produces500()
            .WithTags("External");
    }

    public record GoogleTokenRequest(string Code);
}