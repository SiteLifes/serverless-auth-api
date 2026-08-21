using System.Security.Claims;
using Domain.Constants;
using Domain.Entities;
using Domain.Enum;
using Domain.Services;
using Xunit;

namespace Auth.Tests.Services;

/// <summary>
/// The gateway decides staff access purely from these claims. If one goes missing the session does
/// not fail — it quietly becomes an ordinary resident session, which is how a staff token was once
/// downgraded on refresh without anything erroring.
/// </summary>
public class StaffTokenClaimsTests
{
    [Fact]
    public void Build_ShouldMarkTheTokenAsStaff()
    {
        var claims = StaffTokenClaims.Build(Staff());

        Assert.Equal(
            AuthClaims.UserTypes.Staff,
            claims.Single(c => c.Type == AuthClaims.UserType).Value);
    }

    [Fact]
    public void Build_ShouldCarryTheStaffId()
    {
        var claims = StaffTokenClaims.Build(Staff());

        Assert.Equal("staff-1", claims.Single(c => c.Type == AuthClaims.UserId).Value);
    }

    [Fact]
    public void Build_ShouldEmitOneClaimPerRole()
    {
        var claims = StaffTokenClaims.Build(Staff(StaffRole.Admin, StaffRole.ReadOnly));

        var roles = claims.Where(c => c.Type == AuthClaims.Role).Select(c => c.Value).ToList();

        Assert.Equal(2, roles.Count);
        Assert.Contains("Admin", roles);
        Assert.Contains("ReadOnly", roles);
    }

    [Fact]
    public void Build_ShouldNotRepeatADuplicatedRole()
    {
        var claims = StaffTokenClaims.Build(Staff(StaffRole.Admin, StaffRole.Admin));

        Assert.Single(claims.Where(c => c.Type == AuthClaims.Role));
    }

    [Fact]
    public void Build_ForAnAccountWithNoRole_ShouldStillMarkItStaff()
    {
        // Such an account can sign in and read nothing; it must not fall through to a
        // resident-shaped token.
        var claims = StaffTokenClaims.Build(Staff());

        Assert.Empty(claims.Where(c => c.Type == AuthClaims.Role));
        Assert.Contains(claims, c => c.Type == AuthClaims.UserType);
    }

    [Fact]
    public void Build_ShouldCarryTheNameTheGatewayServesAsAProfile()
    {
        // users/me answers from this claim for staff, who have no user-service record.
        var claims = StaffTokenClaims.Build(Staff());

        Assert.Equal("Sefer Bülbül", claims.Single(c => c.Type == AuthClaims.FullName).Value);
    }

    [Fact]
    public void Build_ShouldKeepTheAuthenticationClaimTheLoginPolicyRequires()
    {
        var claims = StaffTokenClaims.Build(Staff());

        Assert.Contains(claims, c => c.Type == ClaimTypes.Authentication);
    }

    private static StaffEntity Staff(params StaffRole[] roles) => new()
    {
        Id = "staff-1",
        Email = "sefer@sitelifes.com",
        FullName = "Sefer Bülbül",
        Roles = roles.ToList(),
        IsActive = true
    };
}
