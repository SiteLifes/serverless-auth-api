using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Repositories;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Password;

public class Validate : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] ResetPasswordOtpValidateRequest request,
        [FromServices] IAuthService authService,
        [FromServices] IAuthRepository authRepository,
        CancellationToken cancellationToken)
    {
        var userId = await authService.FindUserByEmail(request.Email, cancellationToken);
        if (string.IsNullOrEmpty(userId))
            return Results.NotFound();

        var otpEntity = await authRepository.GetForgotPasswordOtpAsync(request.Email, request.Otp, cancellationToken);
        if (otpEntity == null)
        {
            return Results.Ok(false);
        }

        return Results.Ok(true);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/password/reset/otp/validate", Handler)
            .Produces200<bool>()
            .WithTags("Password");
    }

    public record ResetPasswordOtpValidateRequest(string Email, string Otp);
}