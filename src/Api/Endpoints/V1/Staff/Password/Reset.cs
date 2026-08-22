using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff.Password;

/// <summary>
/// Sets a new password on someone else's staff account, for when they have forgotten theirs.
///
/// Restricted to staff admins by the gateway. This does let an admin take over another account, so
/// it is deliberately not silent: the gateway's staff audit records who called it and on whom, and
/// every session the account had is dropped, so the owner is signed out and finds out.
///
/// The authenticator secret is untouched. Whoever uses the new password still needs the device.
/// </summary>
public class Reset : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string id,
        [FromBody] ResetStaffPasswordRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IStaffPasswordHasher passwordHasher,
        [FromServices] IStaffSessionRevoker sessionRevoker,
        [FromServices] IValidator<ResetStaffPasswordRequest> validator,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        if (!apiContext.IsStaff)
            return Results.Forbid();

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var staff = await staffRepository.GetByIdAsync(id, cancellationToken);
        if (staff is null)
            return Results.NotFound();

        var (hash, salt) = passwordHasher.HashPassword(request.NewPassword);
        staff.PasswordHash = hash;
        staff.PasswordSalt = salt;

        await staffRepository.SaveAsync(staff, cancellationToken);

        var revoked = await sessionRevoker.RevokeAllAsync(staff.Id, cancellationToken);

        return Results.Ok(new ResetStaffPasswordResponse(staff.Id, staff.Email, revoked));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/staff/{id}/password/reset", Handler)
            .Produces<ResetStaffPasswordResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Staff");
    }

    public record ResetStaffPasswordRequest(string NewPassword);

    /// <param name="RevokedSessionCount">
    /// How many sessions the reset ended. Zero means the account was not signed in anywhere, which
    /// is worth seeing when the reset was prompted by something suspicious.
    /// </param>
    public record ResetStaffPasswordResponse(string Id, string Email, int RevokedSessionCount);

    public class ResetStaffPasswordRequestValidator : AbstractValidator<ResetStaffPasswordRequest>
    {
        public ResetStaffPasswordRequestValidator()
        {
            RuleFor(q => q.NewPassword)
                .NotEmpty()
                .MinimumLength(StaffPasswordPolicy.MinimumLength).WithMessage(StaffPasswordPolicy.TooShortMessage);
        }
    }
}
