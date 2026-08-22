using Amazon.DynamoDBv2.Model;
using Domain.Entities;
using Domain.Enum;
using Domain.Services;
using Infrastructure.Extensions;
using Xunit;

namespace Auth.Tests.Repositories;

/// <summary>
/// The gateway authorises staff on the roles claim, and that claim is built from what comes back
/// out of DynamoDB. If roles do not survive the attribute-map round trip the token still looks
/// valid — it just silently carries no roles, and every admin turns into a read-only user.
/// </summary>
public class StaffEntityRoundTripTests
{
    private static Dictionary<string, AttributeValue> StoredItem() => new()
    {
        ["pk"] = new AttributeValue { S = "staff" },
        ["sk"] = new AttributeValue { S = "44aae00c" },
        ["id"] = new AttributeValue { S = "44aae00c" },
        ["email"] = new AttributeValue { S = "someone@sitelifes.com" },
        ["fullName"] = new AttributeValue { S = "Someone" },
        ["passwordHash"] = new AttributeValue { S = "hash" },
        ["passwordSalt"] = new AttributeValue { S = "salt" },
        ["totpSecret"] = new AttributeValue { S = "secret" },
        ["isActive"] = new AttributeValue { BOOL = true },
        // Exactly how the live record stores it: a list of numbers.
        ["roles"] = new AttributeValue
        {
            L = new List<AttributeValue> { new() { N = "2" } }
        }
    };

    [Fact]
    public void Roles_survive_the_attribute_map_round_trip()
    {
        var staff = StoredItem().ToEntity<StaffEntity>();

        Assert.Equal(new[] { StaffRole.Admin }, staff.Roles);
    }

    [Fact]
    public void Token_claims_carry_the_admin_role()
    {
        var staff = StoredItem().ToEntity<StaffEntity>();

        var claims = StaffTokenClaims.Build(staff);

        Assert.Contains(claims, c => c.Type == "roles" && c.Value == "Admin");
    }
}
