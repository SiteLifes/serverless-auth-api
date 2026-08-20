using Api.Infrastructure.Contract;
using Api.Infrastructure.Extensions;
using Domain.Entities;
using Domain.Enum;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

/// <summary>
/// Provisions a staff account. There is no self service sign up for staff: this route is published
/// through the gateway behind the internal api key, and is used to bootstrap the first SuperAdmin.
/// </summary>
public class Post : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromBody] CreateStaffRequest request,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IStaffPasswordHasher passwordHasher,
        [FromServices] ITotpService totpService,
        [FromServices] IValidator<CreateStaffRequest> validator,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
        {
            return Results.NotFound();
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
            return Results.BadRequest(validationResult.ToDictionary());

        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await staffRepository.GetByEmailAsync(email, cancellationToken);
        if (existing is not null)
        {
            return Results.Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "A staff account with this email already exists."
            });
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);

        // The authenticator secret is issued here, at creation, rather than through a later opt in.
        // That removes the window where an account exists but has no second factor.
        var totpSecret = totpService.GenerateSecret();

        var staff = new StaffEntity
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            FullName = request.FullName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            TotpSecret = totpSecret,
            Roles = request.Roles.Distinct().ToList(),
            IsActive = true
        };

        await staffRepository.SaveAsync(staff, cancellationToken);

        // This is the only time the provisioning URI is returned. It is not retrievable later.
        var provisioningUri = totpService.BuildProvisioningUri(
            totpSecret,
            staff.Email,
            staffAuthOptions.Value.TwoFactorIssuer);

        return Results.Created(
            $"/v1/staff/{staff.Id}",
            new CreateStaffResponse(staff.Id, staff.Email, provisioningUri));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/staff", Handler)
            .Produces<CreateStaffResponse>(StatusCodes.Status201Created)
            .WithTags("Staff");
    }

    public record CreateStaffRequest(string Email, string FullName, string Password, List<StaffRole> Roles);

    public record CreateStaffResponse(string Id, string Email, string TotpProvisioningUri);

    public class CreateStaffRequestValidator : AbstractValidator<CreateStaffRequest>
    {
        public CreateStaffRequestValidator()
        {
            RuleFor(q => q.Email).NotEmpty().EmailAddress();
            RuleFor(q => q.FullName).NotEmpty().MaximumLength(200);
            RuleFor(q => q.Password)
                .NotEmpty()
                .MinimumLength(12).WithMessage("Staff passwords must be at least 12 characters.");
            RuleFor(q => q.Roles).NotEmpty().WithMessage("At least one role is required.");
        }
    }
}
