using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Register;

public class ValidateOtp : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] ValidateOtpForRegister request,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<ValidateOtpForRegister> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var result = await authService.VerifyOtpAsync(request.Key, request.Otp, cancellationToken);
        if (result.IsLocked)
        {
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        if (!result.IsSuccess)
        {
            return Results.NotFound();
        }

        var userId = await authService.FindUserByPhone(request.Key, cancellationToken);
        if (!string.IsNullOrEmpty(userId))
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/register/validate-otp", Handler)
            .Produces204()
            .WithTags("Register");
    }

    public record ValidateOtpForRegister(string Key, string Otp);

    public class ValidateOtpForRegisterValidator : AbstractValidator<ValidateOtpForRegister>
    {
        public ValidateOtpForRegisterValidator()
        {
            RuleFor(q => q.Key).NotEmpty();
            RuleFor(q => q.Otp).NotEmpty();
        }
    }
}