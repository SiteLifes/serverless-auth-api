using Api.Infrastructure.Contract;
using Domain.Domains;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Token;

public class RefreshToken : IEndpoint
{
    private static async Task<IResult> Handler([FromBody] RefreshTokenRequest request,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<RefreshTokenRequest> validator,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(validationResult.ToDictionary());
        }

        var userId = await jwtService.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var jwt = await jwtService.CreateJwtAsync(userId, cancellationToken);
        return Results.Ok(new JwtDto(jwt.Token, jwt.RefreshToken));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("/v1/refresh-token", Handler)
            .Produces<JwtDto>()
            .WithTags("Token");
    }

    public record RefreshTokenRequest(string RefreshToken);

    public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
    {
        public RefreshTokenRequestValidator()
        {
            RuleFor(q => q.RefreshToken).NotEmpty();
        }
    }
}