using Api.Infrastructure.Contract;
using Domain.Services;
using FluentValidation;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;

namespace Api.Endpoints.V1.Login.Apply;

public class Otp : IEndpoint
{
    private static async Task<IResult> Handler([FromBody] LoginByPhoneOtp.LoginByPhoneRequest request,
        [FromServices] IAuthService authService,
        [FromServices] IApiContext apiContext,
        [FromServices] IValidator<LoginByPhoneOtp.LoginByPhoneRequest> validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var phone = request.Phone.Replace("+90","");
        var userId = await authService.FindUserByPhone(phone, cancellationToken);
        var isRegistered = !string.IsNullOrEmpty(userId);

        if (isRegistered)
            return Results.NotFound(new ProblemDetails()
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Hata!",
                Detail = "Yetkili Telefon numarası sistemde kayıtlıdır. İlginiz için teşekkür ederiz. "
            });

        var result = await authService.SendLoginOtpAsync(userId, phone, apiContext.Culture, true,
            apiContext.IpAddress, cancellationToken);

        if (result.IsRateLimited)
        {
            httpContext.Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();
            return Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Çok fazla istek",
                detail: $"Lütfen {result.RetryAfterSeconds} saniye sonra tekrar deneyin.");
        }

        return Results.Ok(new LoginByPhoneOtp.LoginByPhoneResponse(phone, isRegistered, result.IsSuccess));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/login/apply/otp", Handler)
            .Produces<LoginByPhoneOtp.LoginByPhoneResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .WithTags("Login");
    }
}
