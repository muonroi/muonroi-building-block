using Muonroi.Core.Abstractions.Exceptions;
using Muonroi.Core.Abstractions.Guards;

namespace Muonroi.Core.Abstractions.Tests.Guards;

public class MGuardDomainTests
{
    // ===== Authorized =====

    [Fact]
    public void Authorized_True_Does_Not_Throw()
    {
        var act = () => MGuard.Authorized(true, "must be authenticated");
        act.Should().NotThrow();
    }

    [Fact]
    public void Authorized_False_Throws_MUnauthorizedException()
    {
        var act = () => MGuard.Authorized(false, "not logged in");
        act.Should().Throw<MUnauthorizedException>()
            .Which.HttpStatusCode.Should().Be(401);
    }

    [Fact]
    public void Authorized_False_Has_Correct_ErrorCode()
    {
        var act = () => MGuard.Authorized(false, "not logged in");
        act.Should().Throw<MUnauthorizedException>()
            .Which.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public void Authorized_False_Has_Correct_Message()
    {
        var act = () => MGuard.Authorized(false, "not logged in");
        act.Should().Throw<MUnauthorizedException>()
            .Which.Message.Should().Be("not logged in");
    }

    [Fact]
    public void Authorized_False_Has_CallerContext()
    {
        try
        {
            MGuard.Authorized(false, "test");
            Assert.Fail("Should have thrown");
        }
        catch (MUnauthorizedException ex)
        {
            ex.CallerMethod.Should().NotBeNullOrEmpty();
            ex.CallerFile.Should().NotBeNullOrEmpty();
            ex.CallerLine.Should().BeGreaterThan(0);
            ex.SourcePackage.Should().NotBeNullOrEmpty();
        }
    }

    // ===== Permitted =====

    [Fact]
    public void Permitted_True_Does_Not_Throw()
    {
        var act = () => MGuard.Permitted(true, "has permission");
        act.Should().NotThrow();
    }

    [Fact]
    public void Permitted_False_Throws_MForbiddenException()
    {
        var act = () => MGuard.Permitted(false, "no access");
        act.Should().Throw<MForbiddenException>()
            .Which.HttpStatusCode.Should().Be(403);
    }

    [Fact]
    public void Permitted_False_Has_Correct_ErrorCode()
    {
        var act = () => MGuard.Permitted(false, "no access");
        act.Should().Throw<MForbiddenException>()
            .Which.ErrorCode.Should().Be("FORBIDDEN");
    }

    [Fact]
    public void Permitted_False_Has_Correct_Message()
    {
        var act = () => MGuard.Permitted(false, "no access");
        act.Should().Throw<MForbiddenException>()
            .Which.Message.Should().Be("no access");
    }

    [Fact]
    public void Permitted_False_Has_CallerContext()
    {
        try
        {
            MGuard.Permitted(false, "test");
            Assert.Fail("Should have thrown");
        }
        catch (MForbiddenException ex)
        {
            ex.CallerMethod.Should().NotBeNullOrEmpty();
            ex.CallerFile.Should().NotBeNullOrEmpty();
            ex.CallerLine.Should().BeGreaterThan(0);
            ex.SourcePackage.Should().NotBeNullOrEmpty();
        }
    }

    // ===== Found =====

    [Fact]
    public void Found_NonNull_Returns_Value()
    {
        var obj = new object();
        var result = MGuard.Found(obj, "Entity");
        result.Should().BeSameAs(obj);
    }

    [Fact]
    public void Found_Null_Throws_MNotFoundException()
    {
        var act = () => MGuard.Found<object>(null, "User");
        act.Should().Throw<MNotFoundException>()
            .Which.HttpStatusCode.Should().Be(404);
    }

    [Fact]
    public void Found_Null_Has_CallerContext()
    {
        try
        {
            MGuard.Found<object>(null, "User");
            Assert.Fail("Should have thrown");
        }
        catch (MNotFoundException ex)
        {
            ex.CallerMethod.Should().NotBeNullOrEmpty();
            ex.CallerFile.Should().NotBeNullOrEmpty();
            ex.CallerLine.Should().BeGreaterThan(0);
            ex.SourcePackage.Should().NotBeNullOrEmpty();
        }
    }

    // ===== NotConflict =====

    [Fact]
    public void NotConflict_False_Does_Not_Throw()
    {
        var act = () => MGuard.NotConflict(false, "no conflict");
        act.Should().NotThrow();
    }

    [Fact]
    public void NotConflict_True_Throws_MConflictException()
    {
        var act = () => MGuard.NotConflict(true, "already exists");
        act.Should().Throw<MConflictException>()
            .Which.HttpStatusCode.Should().Be(409);
    }

    [Fact]
    public void NotConflict_True_Has_CallerContext()
    {
        try
        {
            MGuard.NotConflict(true, "conflict");
            Assert.Fail("Should have thrown");
        }
        catch (MConflictException ex)
        {
            ex.CallerMethod.Should().NotBeNullOrEmpty();
            ex.CallerFile.Should().NotBeNullOrEmpty();
            ex.CallerLine.Should().BeGreaterThan(0);
            ex.SourcePackage.Should().NotBeNullOrEmpty();
        }
    }

    // ===== ValidTenant =====

    [Fact]
    public void ValidTenant_Valid_Returns_Value()
    {
        var result = MGuard.ValidTenant("tenant-1");
        result.Should().Be("tenant-1");
    }

    [Fact]
    public void ValidTenant_Null_Throws_MArgumentException()
    {
        var act = () => MGuard.ValidTenant(null);
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void ValidTenant_Empty_Throws_MArgumentException()
    {
        var act = () => MGuard.ValidTenant("");
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void ValidTenant_Whitespace_Throws_MArgumentException()
    {
        var act = () => MGuard.ValidTenant("  ");
        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void ValidTenant_Null_Has_CallerContext()
    {
        try
        {
            MGuard.ValidTenant(null);
            Assert.Fail("Should have thrown");
        }
        catch (MArgumentException ex)
        {
            // MArgumentException stores caller context in Details (existing pattern)
            ex.Details["CallerMember"].Should().NotBeNull();
            ex.Details["CallerFile"].Should().NotBeNull();
            ex.Details["CallerLine"].Should().NotBeNull();
        }
    }
}
