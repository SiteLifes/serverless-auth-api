using Domain.Enum;
using Domain.Services;
using Xunit;

namespace Auth.Tests.Services;

public class StaffRoleChangeTests
{
    [Fact]
    public void Check_ShouldRefuseChangingYourOwnRoles()
    {
        // Otherwise the last super admin can demote themselves and nobody is left to undo it.
        var refusal = StaffRoleChange.Check("staff-1", "staff-1", [StaffRole.ReadOnly]);

        Assert.Equal(StaffRoleChange.Refusal.OwnAccount, refusal);
    }

    [Fact]
    public void Check_ShouldRefuseOwnAccountEvenWhenPromoting()
    {
        var refusal = StaffRoleChange.Check("staff-1", "staff-1", [StaffRole.SuperAdmin]);

        Assert.Equal(StaffRoleChange.Refusal.OwnAccount, refusal);
    }

    [Fact]
    public void Check_ShouldRefuseAnEmptyRoleList()
    {
        Assert.Equal(StaffRoleChange.Refusal.NoRole, StaffRoleChange.Check("staff-1", "staff-2", []));
        Assert.Equal(StaffRoleChange.Refusal.NoRole, StaffRoleChange.Check("staff-1", "staff-2", null));
    }

    [Fact]
    public void Check_ShouldRefuseARoleNoPolicyKnows()
    {
        var refusal = StaffRoleChange.Check("staff-1", "staff-2", [(StaffRole)7]);

        Assert.Equal(StaffRoleChange.Refusal.UnknownRole, refusal);
    }

    [Fact]
    public void Check_ShouldAcceptAnotherAccountWithKnownRoles()
    {
        var refusal = StaffRoleChange.Check("staff-1", "staff-2", [StaffRole.Admin]);

        Assert.Equal(StaffRoleChange.Refusal.None, refusal);
    }

    [Fact]
    public void Normalize_ShouldDropDuplicatesAndOrderByLevel()
    {
        var roles = StaffRoleChange.Normalize([StaffRole.SuperAdmin, StaffRole.ReadOnly, StaffRole.SuperAdmin]);

        Assert.Equal([StaffRole.ReadOnly, StaffRole.SuperAdmin], roles);
    }

    [Fact]
    public void IsUnchanged_ShouldIgnoreOrderAndDuplicates()
    {
        Assert.True(StaffRoleChange.IsUnchanged(
            [StaffRole.Admin, StaffRole.ReadOnly],
            [StaffRole.ReadOnly, StaffRole.Admin, StaffRole.Admin]));

        Assert.False(StaffRoleChange.IsUnchanged([StaffRole.Admin], [StaffRole.SuperAdmin]));
    }
}
