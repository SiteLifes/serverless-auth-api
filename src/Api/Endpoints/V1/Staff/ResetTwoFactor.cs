using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Domain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff;

/// <summary>
/// Issues a fresh authenticator secret for a staff account, for when someone loses their device.
/// Published behind the internal api key: it is a credential reset, not a self service action.
/// </summary>
public class ResetTwoFactor : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string id,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] ITotpService totpService,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
        {
            return Results.NotFound();
        }

        var staff = await staffRepository.GetByIdAsync(id, cancellationToken);
        if (staff is null)
        {
            return Results.NotFound();
        }

        staff.TotpSecret = totpService.GenerateSecret();
        await staffRepository.SaveAsync(staff, cancellationToken);

        var provisioningUri = totpService.BuildProvisioningUri(
            staff.TotpSecret,
            staff.Email,
            staffAuthOptions.Value.TwoFactorIssuer);

        return Results.Ok(new ResetTwoFactorResponse(staff.Id, provisioningUri));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost("v1/staff/{id}/2fa/reset", Handler)
            .Produces<ResetTwoFactorResponse>()
            .WithTags("Staff");
    }

    public record ResetTwoFactorResponse(string Id, string TotpProvisioningUri);
}
