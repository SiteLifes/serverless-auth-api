using Api.Infrastructure.Contract;
using Domain.Enum;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff.Roles;

/// <summary>
/// Changes the roles on someone else's staff account. Restricted to super admins by the gateway.
///
/// Sessions are left alone: the refresh endpoint rebuilds the token from the record, so the new
/// roles apply on the holder's next refresh. Revoking would not be faster — an access token already
/// issued stays valid until it expires either way — it would only sign them out.
/// </summary>
public class Put : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string id,
        [FromBody] UpdateStaffRolesRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        if (!apiContext.IsStaff)
            return Results.Forbid();

        var callerId = apiContext.CurrentUserIdOrNull;
        if (string.IsNullOrEmpty(callerId))
            return Results.Unauthorized();

        switch (StaffRoleChange.Check(callerId, id, request.Roles))
        {
            case StaffRoleChange.Refusal.OwnAccount:
                return Results.Problem(
                    "Staff cannot change their own roles.",
                    statusCode: StatusCodes.Status409Conflict);
            case StaffRoleChange.Refusal.NoRole:
                return Results.Problem(
                    "At least one role is required.",
                    statusCode: StatusCodes.Status400BadRequest);
            case StaffRoleChange.Refusal.UnknownRole:
                return Results.Problem(
                    "Unknown role.",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        var staff = await staffRepository.GetByIdAsync(id, cancellationToken);
        if (staff is null)
            return Results.NotFound();

        if (!StaffRoleChange.IsUnchanged(staff.Roles, request.Roles))
        {
            staff.Roles = StaffRoleChange.Normalize(request.Roles);
            staff.UpdatedAt = DateTime.UtcNow;

            await staffRepository.SaveAsync(staff, cancellationToken);
        }

        return Results.Ok(new UpdateStaffRolesResponse(
            staff.Id,
            staff.Email,
            staff.Roles.Select(role => role.ToString()).ToList()));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("v1/staff/{id}/roles", Handler)
            .Produces<UpdateStaffRolesResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Staff");
    }

    public record UpdateStaffRolesRequest(List<StaffRole> Roles);

    /// <param name="Roles">Named the way the staff list names them, so the panel reads both alike.</param>
    public record UpdateStaffRolesResponse(string Id, string Email, List<string> Roles);
}
