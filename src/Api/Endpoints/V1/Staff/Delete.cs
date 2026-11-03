using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

/// <summary>
/// Removes someone else's staff account for good. Restricted to super admins by the gateway.
///
/// The gateway's audit rows are keyed by staff id and stay where they are; only the account and its
/// email lookup go, so the address can be provisioned again. Deactivating is the reversible option.
/// </summary>
public class Delete : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string id,
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
                "Staff cannot delete their own account.",
                statusCode: StatusCodes.Status409Conflict);

        var staff = await staffRepository.GetByIdAsync(id, cancellationToken);
        if (staff is null)
            return Results.NotFound();

        // Sessions first: a refresh landing between the two would find no record and fall through
        // to the resident path, which would happily mint a token for the id.
        await sessionRevoker.RevokeAllAsync(staff.Id, cancellationToken);
        await staffRepository.DeleteAsync(staff, cancellationToken);

        return Results.NoContent();
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapDelete("v1/staff/{id}", Handler)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithTags("Staff");
    }
}
