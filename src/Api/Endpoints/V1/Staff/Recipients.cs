using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

/// <summary>
/// Who to tell: the active staff accounts with the contact details notifications need (izin talebi
/// süper yöneticilere, kararı personele). Read by the Property service with the internal key; the
/// gateway publishes it behind StaffAdminOrInternalPolicy, like provisioning.
///
/// Nothing secret is returned, and passive accounts are left out: they no longer get anything.
/// </summary>
public class Recipients : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        var staff = await staffRepository.GetAllAsync(cancellationToken);

        var result = staff
            .Where(entity => entity.IsActive)
            .Select(entity => new StaffRecipient(
                entity.Id,
                entity.FullName,
                entity.Email,
                entity.Phone,
                entity.Roles.Select(role => role.ToString()).ToList()))
            .OrderBy(item => item.FullName, StringComparer.CurrentCulture)
            .ToList();

        return Results.Ok(result);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("v1/staff/recipients", Handler)
            .Produces<List<StaffRecipient>>()
            .WithTags("Staff");
    }

    public record StaffRecipient(
        string Id,
        string FullName,
        string Email,
        string? Phone,
        List<string> Roles);
}
