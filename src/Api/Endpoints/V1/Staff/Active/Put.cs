using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff.Active;

/// <summary>
/// Turns someone else's staff account off or back on. Restricted to super admins by the gateway.
///
/// Login and refresh already refuse an inactive account; deactivating also drops every session, so
/// the holder is out once their current access token expires instead of on their next sign-in.
/// Never your own account, for the same reason roles are not: the last super admin could lock
/// everyone out.
/// </summary>
public class Put : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string id,
        [FromBody] UpdateStaffActiveRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IStaffSessionRevoker sessionRevoker,
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

        if (string.Equals(callerId, id, StringComparison.Ordinal))
            return Results.Problem(
                "Staff cannot deactivate their own account.",
                statusCode: StatusCodes.Status409Conflict);

        var staff = await staffRepository.GetByIdAsync(id, cancellationToken);
        if (staff is null)
            return Results.NotFound();

        var revoked = 0;
        if (staff.IsActive != request.IsActive)
        {
            staff.IsActive = request.IsActive;
            await staffRepository.SaveAsync(staff, cancellationToken);
        }

        if (!staff.IsActive)
            revoked = await sessionRevoker.RevokeAllAsync(staff.Id, cancellationToken);

        return Results.Ok(new UpdateStaffActiveResponse(staff.Id, staff.Email, staff.IsActive, revoked));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("v1/staff/{id}/active", Handler)
            .Produces<UpdateStaffActiveResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Staff");
    }

    public record UpdateStaffActiveRequest(bool IsActive);

    public record UpdateStaffActiveResponse(string Id, string Email, bool IsActive, int RevokedSessionCount);
}
