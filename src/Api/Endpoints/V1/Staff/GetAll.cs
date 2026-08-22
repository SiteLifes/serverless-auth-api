using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

/// <summary>
/// Who holds a panel account. Answers "who can get in", which is the question an admin has before
/// adding one more.
///
/// Nothing secret is returned: the password hash, its salt and the authenticator secret stay on the
/// record. The gateway restricts this to staff tokens; the check here is the second lock.
/// </summary>
public class GetAll : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromServices] IApiContext apiContext,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        if (!apiContext.IsStaff)
            return Results.Forbid();

        var staff = await staffRepository.GetAllAsync(cancellationToken);

        var result = staff
            .Select(entity => new StaffListItem(
                entity.Id,
                entity.Email,
                entity.FullName,
                entity.Roles.Select(role => role.ToString()).ToList(),
                entity.IsActive,
                entity.LastLoginAt,
                entity.CreatedAt))
            .OrderBy(item => item.FullName, StringComparer.CurrentCulture)
            .ToList();

        return Results.Ok(result);
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapGet("v1/staff", Handler)
            .Produces<List<StaffListItem>>()
            .Produces(StatusCodes.Status403Forbidden)
            .WithTags("Staff");
    }

    public record StaffListItem(
        string Id,
        string Email,
        string FullName,
        List<string> Roles,
        bool IsActive,
        DateTime? LastLoginAt,
        DateTime CreatedAt);
}
