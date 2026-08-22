using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff.Me.Password;

/// <summary>
/// Lets a staff member replace their own password.
///
/// Accounts are provisioned with a password chosen by whoever created them, so without this the
/// first password is the only password, and it is known to someone else for the life of the
/// account. Proving the current password is what authorises the change; no admin can set another
/// person's password through here.
/// </summary>
public class Post : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] ChangeStaffPasswordRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IStaffSessionRevoker sessionRevoker,
        [FromServices] IStaffPasswordHasher passwordHasher,
        [FromServices] IValidator<ChangeStaffPasswordRequest> validator,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        if (!apiContext.IsStaff)
            return Results.Forbid();

        var staffId = apiContext.CurrentUserIdOrNull;
        if (string.IsNullOrWhiteSpace(staffId))
            return Results.Unauthorized();

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var staff = await staffRepository.GetByIdAsync(staffId, cancellationToken);
        if (staff is null || !staff.IsActive)
            return Results.Unauthorized();

        if (!passwordHasher.Verify(request.CurrentPassword, staff.PasswordHash, staff.PasswordSalt))
            return Results.Unauthorized();

        var (hash, salt) = passwordHasher.HashPassword(request.NewPassword);
        staff.PasswordHash = hash;
        staff.PasswordSalt = salt;

        await staffRepository.SaveAsync(staff, cancellationToken);

        await sessionRevoker.RevokeAllAsync(staff.Id, cancellationToken);

        return Results.NoContent();
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/staff/me/password", Handler)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Staff");
    }

    public record ChangeStaffPasswordRequest(string CurrentPassword, string NewPassword);

    public class ChangeStaffPasswordRequestValidator : AbstractValidator<ChangeStaffPasswordRequest>
    {
        public ChangeStaffPasswordRequestValidator()
        {
            RuleFor(q => q.CurrentPassword).NotEmpty();
            RuleFor(q => q.NewPassword)
                .NotEmpty()
                .MinimumLength(12).WithMessage("Staff passwords must be at least 12 characters.")
                .NotEqual(q => q.CurrentPassword).WithMessage("The new password must differ from the current one.");
        }
    }
}
