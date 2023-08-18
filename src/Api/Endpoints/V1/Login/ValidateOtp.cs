using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Domains;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Login;

public class ValidateOtp : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] ValidateOtpRequestModel request,
        [FromServices] IAuthService authService,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<ValidateOtpRequestModel> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var result = await authService.VerifyOtpAsync(request.Key, request.Otp, cancellationToken);
        if (!result)
        {
            return Results.NotFound();
        }

        var userId = await authService.FindUserByPhone(request.Key, cancellationToken);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.NotFound();
        }

        var jwt = await jwtService.CreateJwtAsync(userId, cancellationToken);

        return Results.Ok(jwt);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/login/validate-otp", Handler)
            .Produces200<JwtDto>()
            .WithTags("Login");
    }

    public record ValidateOtpRequestModel(string Key, string Otp);

    public class ValidateOtpRequestModelValidator : AbstractValidator<ValidateOtpRequestModel>
    {
        public ValidateOtpRequestModelValidator()
        {
            RuleFor(q => q.Key).NotEmpty();
            RuleFor(q => q.Otp).NotEmpty();
        }
    }
}