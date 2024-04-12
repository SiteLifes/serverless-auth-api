using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Services;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Logout;

public class Post : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] LogoutRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IAuthService authService,
        [FromServices] IEventBusManager eventBusManager,
        CancellationToken cancellationToken)
    {
        await authService.DeleteRefreshTokenAsync(request.RefreshToken, cancellationToken);
        await eventBusManager.LogoutUserAsync(apiContext.CurrentUserId, request.DeviceToken ?? "", cancellationToken);
        return Results.Ok();
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/logout", Handler)
            .Produces200()
            .Produces400()
            .WithTags("User");
    }

    public record LogoutRequest(string RefreshToken, string? DeviceToken);
}