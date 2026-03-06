namespace Muonroi.BuildingBlock.Test;

public class MPermissionAuditLogTests
{
    [Fact]
    public void RoleId_Get_Returns_Value()
    {
        Guid id = Guid.NewGuid();
        MPermissionAuditLog log = new()
        {
            RoleId = id
        };
        Assert.Equal(id, log.RoleId);

        log = new MPermissionAuditLog();
        Assert.Equal(Guid.Empty, log.RoleId);
    }

    [Fact]
    public void PermissionId_Get_Returns_Value()
    {
        Guid id = Guid.NewGuid();
        MPermissionAuditLog log = new()
        {
            PermissionId = id
        };
        Assert.Equal(id, log.PermissionId);

        log = new MPermissionAuditLog();
        Assert.Equal(Guid.Empty, log.PermissionId);
    }

    [Fact]
    public void Action_Get_Returns_Value()
    {
        MPermissionAuditLog log = new()
        {
            Action = "act"
        };
        Assert.Equal("act", log.Action);

        log = new MPermissionAuditLog();
        Assert.Equal(string.Empty, log.Action);
    }

    [Fact]
    public void PerformedBy_Get_Returns_Value()
    {
        Guid id = Guid.NewGuid();
        MPermissionAuditLog log = new()
        {
            PerformedBy = id
        };
        Assert.Equal(id, log.PerformedBy);

        log = new MPermissionAuditLog();
        Assert.Null(log.PerformedBy);
    }
}

public class MRoleTests
{
    [Fact]
    public void IsStatic_Get_Returns_Value()
    {
        MRole role = new()
        {
            IsStatic = true
        };
        Assert.True(role.IsStatic);

        role = new MRole();
        Assert.False(role.IsStatic);
    }

    [Fact]
    public void IsDefault_Get_Returns_Value()
    {
        MRole role = new()
        {
            IsDefault = true
        };
        Assert.True(role.IsDefault);

        role = new MRole();
        Assert.False(role.IsDefault);
    }
}

public class MUserTests
{
    [Fact]
    public void FullName_Get_Returns_Value()
    {
        MUser user = new()
        {
            Name = "John",
            Surname = "Doe"
        };
        Assert.Equal("John Doe", user.FullName);

        user = new MUser();
        Assert.Equal(" ", user.FullName);
    }

    [Fact]
    public void PasswordResetCode_Get_Returns_Value()
    {
        MUser user = new()
        {
            PasswordResetCode = "code"
        };
        Assert.Equal("code", user.PasswordResetCode);

        user = new MUser();
        Assert.Null(user.PasswordResetCode);
    }

    [Fact]
    public void ProfilePictureId_Get_Returns_Value()
    {
        MUser user = new()
        {
            ProfilePictureId = 1
        };
        Assert.Equal(1, user.ProfilePictureId);

        user = new MUser();
        Assert.Null(user.ProfilePictureId);
    }

    [Fact]
    public void ShouldChangePasswordOnNextLogin_Get_Returns_Value()
    {
        MUser user = new()
        {
            ShouldChangePasswordOnNextLogin = true
        };
        Assert.True(user.ShouldChangePasswordOnNextLogin);

        user = new MUser();
        Assert.False(user.ShouldChangePasswordOnNextLogin);
    }
}
