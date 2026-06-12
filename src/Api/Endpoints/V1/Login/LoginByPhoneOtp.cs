using Api.Infrastructure.Contract;
using Domain.Services;
using FluentValidation;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Login;

public class LoginByPhoneOtp : IEndpoint
{
    private static async Task<IResult> Handler([FromBody] LoginByPhoneRequest request,
        [FromServices] IAuthService authService,
        [FromServices] IApiContext apiContext,
        [FromServices] ICaptchaService captchaService,
        [FromServices] IValidator<LoginByPhoneRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        // var isCaptchaValid = await captchaService.ValidateAsync(request.CaptchaToken, apiContext.IpAddress, cancellationToken);
        // if (!isCaptchaValid)
        //     return Results.BadRequest(new Dictionary<string, string> {{"Captcha", "Captcha is not valid"}});


        var phone = request.Phone.Replace("+90","");
        var userId = await authService.FindUserByPhone(phone, cancellationToken);
        var isRegistered = !string.IsNullOrEmpty(userId);

        if (!isRegistered)
            return Results.NotFound(new ProblemDetails()
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Hata!",
                Detail = "Telefon numaranıza kayıtlı bir site bulunamadı."
            });

        var result = await authService.SendLoginOtpAsync(userId, phone, apiContext.Culture, isRegistered,
            apiContext.IpAddress, cancellationToken);

        if (result.IsRateLimited)
        {
            httpContext.Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();
            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Çok fazla istek",
                detail: $"Lütfen {result.RetryAfterSeconds} saniye sonra tekrar deneyin.");
        }


        return Results.Ok(new LoginByPhoneResponse(phone, isRegistered, result.IsSuccess));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/login/phone/otp", Handler)
            .Produces<LoginByPhoneResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithTags("Login");
    }

    public record LoginByPhoneRequest(string Phone, string CaptchaToken);

    public record LoginByPhoneResponse(string Phone, bool IsRegistered, bool OtpSent);

    public class LoginByPhoneRequestValidator : AbstractValidator<LoginByPhoneRequest>
    {
        public LoginByPhoneRequestValidator()
        {
            RuleFor(q => q.Phone).NotEmpty();
        }
    }
}
