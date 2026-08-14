using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Phone;

public class Put : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string userId,
        [FromBody] UpdateUserPhoneMappingRequest request,
        [FromServices] IAuthService authService,
        CancellationToken cancellationToken)
    {
        var mappingUpdated = await authService.UpdateUserPhoneMappingAsync(
            userId,
            request.OldPhone,
            request.Phone,
            cancellationToken);
        if (!mappingUpdated)
        {
            return Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Telefon numarası kullanımda",
                Detail = "Bu telefon numarası başka bir kullanıcı hesabına bağlı."
            });
        }

        return Results.Ok();
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("/v1/users/{userId}/phone", Handler)
            .Produces200()
            .Produces400()
            .Produces404()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .Produces500()
            .WithTags("User");
    }

    public record UpdateUserPhoneMappingRequest(string? OldPhone, string Phone);
}
