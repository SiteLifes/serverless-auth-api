using Api.Infrastructure.Contract;
using Domain.Options;
using Domain.Repositories;
using Infrastructure.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Endpoints.V1.Staff.Phone;

/// <summary>
/// Sets or clears the mobile number on a staff account. Restricted to super admins by the gateway.
///
/// The number is where reminders are pushed (a sales call the staff member planned), through the
/// SiteLifes app account opened with it. Like a role change, it reaches the token on the holder's
/// next refresh, which rebuilds the claims from the record.
/// </summary>
public class Put : IEndpoint
{
    private static async Task<IResult> Handler(
        [FromRoute] string id,
        [FromBody] UpdateStaffPhoneRequest request,
        [FromServices] IApiContext apiContext,
        [FromServices] IStaffRepository staffRepository,
        [FromServices] IOptionsSnapshot<StaffAuthOptions> staffAuthOptions,
        CancellationToken cancellationToken)
    {
        if (!staffAuthOptions.Value.Enabled)
            return Results.NotFound();

        if (!apiContext.IsStaff)
            return Results.Forbid();

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(request.Phone))
        {
            phone = StaffPhone.Normalize(request.Phone);
            if (phone is null)
                return Results.Problem(
                    "Geçerli bir cep telefonu girin (05xx xxx xx xx).",
                    statusCode: StatusCodes.Status400BadRequest);
        }

        var staff = await staffRepository.GetByIdAsync(id, cancellationToken);
        if (staff is null)
            return Results.NotFound();

        if (staff.Phone != phone)
        {
            staff.Phone = phone;
            staff.UpdatedAt = DateTime.UtcNow;

            await staffRepository.SaveAsync(staff, cancellationToken);
        }

        return Results.Ok(new UpdateStaffPhoneResponse(staff.Id, staff.Phone));
    }

    public RouteHandlerBuilder MapEndpoint(IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPut("v1/staff/{id}/phone", Handler)
            .Produces<UpdateStaffPhoneResponse>()
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithTags("Staff");
    }

    /// <param name="Phone">Empty or null clears it.</param>
    public record UpdateStaffPhoneRequest(string? Phone);

    public record UpdateStaffPhoneResponse(string Id, string? Phone);
}

/// <summary>Turkish mobile as ten digits ("5xxxxxxxxx"); anything else is refused.</summary>
public static class StaffPhone
{
    public static string? Normalize(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());

        if (digits.Length == 12 && digits.StartsWith("90"))
            digits = digits[2..];
        if (digits.Length == 11 && digits.StartsWith('0'))
            digits = digits[1..];

        return digits.Length == 10 && digits[0] == '5' ? digits : null;
    }
}
