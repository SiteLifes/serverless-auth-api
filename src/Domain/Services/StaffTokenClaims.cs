using System.Security.Claims;
using Domain.Constants;
using Domain.Entities;

namespace Domain.Services;

/// <summary>
/// The claims a staff token carries.
///
/// The gateway authorises entirely on these, so building them lives apart from token signing and
/// is tested directly: a missing userType or roles claim does not fail loudly, it silently turns a
/// staff session into an ordinary one.
/// </summary>
public static class StaffTokenClaims
{
    public static List<Claim> Build(StaffEntity staff)
    {
        var claims = new List<Claim>
        {
            new(AuthClaims.UserId, staff.Id),
            new(AuthClaims.UserType, AuthClaims.UserTypes.Staff),
            new(AuthClaims.FullName, staff.FullName),
            new(ClaimTypes.Actor, "StaffLogin"),
            new(ClaimTypes.Authentication, "Login"),
            new(ClaimTypes.UserData, staff.Id)
        };

        claims.AddRange(staff.Roles
            .Distinct()
            .Select(role => new Claim(AuthClaims.Role, role.ToString())));

        return claims;
    }
}
