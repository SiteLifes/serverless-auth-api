using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Domains;
using Domain.Entities;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Login;

public class ValidateOtp : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] ValidateOtpRequestModel request,
        [FromServices] IAuthService authService,
        [FromServices] IAuthRepository repository,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<ValidateOtpRequestModel> validator,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var phone = request.Key.Replace("+90", "");

        var result = await authService.VerifyOtpAsync(phone, request.Otp, cancellationToken);

        if (!result)
        {
            return Results.Problem(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Geçersiz OTP kodu!",
                Detail = "Lütfen geçerli bir OTP kodu giriniz.",
            });
        }

        var userId = await authService.FindUserByPhone(phone, cancellationToken);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.NotFound();
        }
        
        repository.UserLoginAsync(new UserLoginEntity { UserId = userId }, cancellationToken);
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