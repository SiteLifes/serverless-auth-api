using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

/// <summary>
/// Mints a login OTP for a resident's phone and hands it back to the staff member instead of
/// sending it anywhere.
///
/// Support needs a working code when the resident's own text never arrives — a wrong operator
/// route, a blocked number, a handset that is not with them. The normal
/// <c>v1/login/phone/otp</c> cannot serve that: it always texts the number it was asked about.
/// So this one skips both deliveries, the SMS and the <c>LoginOtpRequested</c> event the
/// notification service listens to, and returns the code in the response body.
///
/// The code it writes is an ordinary login OTP: five minutes, single phone, redeemed at
/// <c>v1/login/validate-otp</c> like any other. Codes are stored under the code itself, so
/// issuing one here does not invalidate one the resident already holds.
///
/// This is account access, so the gateway keeps it on StaffAdminPolicy and the check below is the
/// second lock. Deliberately outside the OTP send rate limiter: consuming a resident's send
/// budget from the back office would lock them out of their own login.
/// </summary>
public class IssueLoginOtp : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] IssueLoginOtpRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IAuthService authService,
        [FromServices] IAuthRepository authRepository,
        [FromServices] IValidator<IssueLoginOtpRequest> validator,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        [FromServices] ILogger<IssueLoginOtp> logger,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        if (!apiContext.IsStaff)
            return Results.Forbid();

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        // Same normalisation the login endpoints use, so the code lands under the key
        // validate-otp will look it up by.
        var phone = request.Phone.Replace("+90", "");

        var userId = await authService.FindUserByPhone(phone, cancellationToken);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Hata!",
                Detail = "Bu telefon numarasına kayıtlı bir kullanıcı bulunamadı."
            });
        }

        var otpEntity = await authRepository.CreateLoginOtpAsync(userId, phone, cancellationToken);

        // Warning, not information: a code that reaches a staff member rather than the account
        // holder is worth finding in the logs later, whoever asks the question.
        logger.LogWarning(
            "Staff {StaffId} issued an undelivered login OTP for user {UserId} (phone ending {PhoneSuffix}).",
            apiContext.CurrentUserIdOrNull,
            userId,
            phone.Length <= 4 ? phone : phone[^4..]);

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(otpEntity.Ttl).UtcDateTime;

        return Results.Ok(new IssueLoginOtpResponse(phone, otpEntity.Otp, expiresAt));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/staff/users/login-otp", Handler)
            .Produces<IssueLoginOtpResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithTags("Staff");
    }

    public record IssueLoginOtpRequest(string Phone);

    /// <param name="ExpiresAt">UTC. The panel counts down against it rather than assuming five minutes.</param>
    public record IssueLoginOtpResponse(string Phone, string Otp, DateTime ExpiresAt);

    public class IssueLoginOtpRequestValidator : AbstractValidator<IssueLoginOtpRequest>
    {
        public IssueLoginOtpRequestValidator()
        {
            RuleFor(q => q.Phone).NotEmpty();
        }
    }
}
