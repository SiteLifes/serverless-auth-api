using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Domains;
using Domain.Entities;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

public class Login : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] StaffLoginRequest request,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IStaffPasswordHasher passwordHasher,
        [FromServices] ITotpService totpService,
        [FromServices] IAuthRepository authRepository,
        [FromServices] IJwtService jwtService,
        [FromServices] IValidator<StaffLoginRequest> validator,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        [FromServices] IOptionsSnapshot<OtpSecurityOptions> otpSecurityOptions,
        CancellationToken cancellationToken)
    {
        var options = staffAuthOptions.Value;
        if (!options.Enabled)
        {
            return Results.NotFound();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var email = request.Email.Trim().ToLowerInvariant();
        var attemptKey = $"staff-login:{email}";

        var attempts = await authRepository.GetOtpAttemptAsync(attemptKey, cancellationToken)
                       ?? new OtpAttemptEntity { Key = attemptKey };

        var now = DateTime.UtcNow;
        if (attempts.LockedUntilUtc.HasValue && attempts.LockedUntilUtc.Value > now)
        {
            return LockedResult();
        }

        if (attempts.LockedUntilUtc.HasValue && attempts.LockedUntilUtc.Value <= now)
        {
            attempts.LockedUntilUtc = null;
            attempts.FailedAttempts = 0;
        }

        var staff = await staffRepository.GetByEmailAsync(email, cancellationToken);

        var isPasswordValid = staff is not null
                              && staff.IsActive
                              && passwordHasher.Verify(request.Password, staff.PasswordHash, staff.PasswordSalt);

        var isSecondFactorValid = !options.RequireTwoFactor
                                  || (staff is not null
                                      && totpService.VerifyCode(staff.TotpSecret, request.Code ?? string.Empty));

        // Both factors are evaluated before branching, so a wrong password and a wrong code cost the
        // same and neither can be distinguished from an unknown account.
        if (!isPasswordValid || !isSecondFactorValid)
        {
            attempts.FailedAttempts++;

            if (attempts.FailedAttempts >= otpSecurityOptions.Value.MaxFailedAttempts)
            {
                attempts.LockedUntilUtc = now.AddMinutes(otpSecurityOptions.Value.LockoutMinutes);
                await authRepository.UpsertOtpAttemptAsync(attempts, cancellationToken);
                return LockedResult();
            }

            await authRepository.UpsertOtpAttemptAsync(attempts, cancellationToken);
            return InvalidCredentialsResult();
        }

        await authRepository.DeleteOtpAttemptAsync(attemptKey, cancellationToken);

        staff!.LastLoginAt = now;
        await staffRepository.SaveAsync(staff, cancellationToken);

        var jwt = await jwtService.CreateStaffJwtAsync(staff, cancellationToken);
        return Results.Ok(jwt);
    }

    private static IResult InvalidCredentialsResult() => Results.Problem(new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Unauthorized",
        Detail = "Invalid email, password or verification code."
    });

    private static IResult LockedResult() => Results.Problem(new ProblemDetails
    {
        Status = StatusCodes.Status423Locked,
        Title = "Locked",
        Detail = "Too many failed attempts. Try again later."
    });

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/staff/login", Handler)
            .Produces200<JwtDto>()
            .WithTags("Staff");
    }

    /// <summary>
    /// The authenticator code travels with the credentials rather than in a second call, so there is
    /// no half authenticated state and no intermediate token to steal.
    /// </summary>
    public record StaffLoginRequest(string Email, string Password, string? Code);

    public class StaffLoginRequestValidator : AbstractValidator<StaffLoginRequest>
    {
        public StaffLoginRequestValidator()
        {
            RuleFor(q => q.Email).NotEmpty().EmailAddress();
            RuleFor(q => q.Password).NotEmpty();
        }
    }
}
