using Api.Infrastructure.Contract;
using Domain.Services;
using FluentValidation;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Register;

public class RequestOtp : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] RequestOtpModel request,
        [FromServices] IAuthService authService,
        [FromServices] ICaptchaService captchaService,
        [FromServices] IApiContext apiContext,
        [FromServices] IValidator<RequestOtpModel> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
            return Results.ValidationProblem(validation.ToDictionary());

        if (!await captchaService.ValidateAsync(request.CaptchaToken, apiContext.IpAddress, cancellationToken))
            return Results.BadRequest(new Dictionary<string, string> { ["Captcha"] = "Captcha is not valid" });

        var phone = NormalizePhone(request.Phone);
        if (!string.IsNullOrEmpty(await authService.FindUserByPhone(phone, cancellationToken)))
            return Results.Conflict(new ProblemDetails { Title = "Telefon numarası kullanımda" });

        var result = await authService.SendLoginOtpAsync(null, phone, apiContext.Culture, false,
            apiContext.IpAddress, cancellationToken);
        if (result.IsRateLimited)
        {
            httpContext.Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        return result.IsSuccess ? Results.Ok(new { Phone = phone, OtpSent = true })
            : Results.Problem(statusCode: StatusCodes.Status502BadGateway, title: "OTP gönderilemedi");
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost("v1/register/request-otp", Handler)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status429TooManyRequests)
            .WithTags("Register");

    public record RequestOtpModel(string Phone, string CaptchaToken);

    public class RequestOtpModelValidator : AbstractValidator<RequestOtpModel>
    {
        public RequestOtpModelValidator()
        {
            RuleFor(x => x.Phone).NotEmpty()
                .Must(x => x is not null && System.Text.RegularExpressions.Regex.IsMatch(NormalizePhone(x), @"^5\d{9}$"));
            RuleFor(x => x.CaptchaToken).NotEmpty();
        }
    }

    private static string NormalizePhone(string phone) => phone.Trim()
        .Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "")
        .Replace("+90", "").TrimStart('0');
}
