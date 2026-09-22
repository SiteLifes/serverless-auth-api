using Api.Infrastructure.Contract;
using Domain.Domains;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Token;

public class RefreshToken : IEndpoint
{
    private static async Task<IResult> Handler([FromBody] RefreshTokenRequest request,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IValidator<RefreshTokenRequest> validator,
        [FromServices] ILogger<RefreshToken> logger,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.BadRequest(validationResult.ToDictionary());
        }

        var validation = await jwtService.ValidateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (validation.UserId is null)
        {
            // Never log the token itself: a still-valid one in the logs is a usable credential.
            logger.LogWarning("Refresh token rejected: {Reason}.", validation.Failure);
            return Results.Unauthorized();
        }

        var userId = validation.UserId;

        // A refresh has to re-issue the same kind of token the holder logged in with. Falling back
        // to the resident path here would silently downgrade a staff session on its first refresh,
        // stripping the userType and roles claims the gateway authorises against.
        var staff = await staffRepository.GetByIdAsync(userId, cancellationToken);
        if (staff is not null)
        {
            if (!staff.IsActive)
            {
                logger.LogWarning("Refresh refused for deactivated staff account {StaffId}.", userId);
                return Results.Unauthorized();
            }

            // Roles are read from the record, so a role change takes effect on the next refresh.
            var staffJwt = await jwtService.CreateStaffJwtAsync(staff, cancellationToken, request.RefreshToken);
            return Results.Ok(new JwtDto(staffJwt.Token, staffJwt.RefreshToken));
        }

        var jwt = await jwtService.CreateJwtAsync(userId, cancellationToken, request.RefreshToken);
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
