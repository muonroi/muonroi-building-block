using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.Core.Abstractions.Tests;

public class MenuMetadataTests
{
    [Fact]
    public void MenuMetadata_DefaultValues_AreEmpty()
    {
        MenuMetadata meta = new();

        meta.GroupName.Should().BeEmpty();
        meta.GroupDisplayName.Should().BeEmpty();
        meta.Actions.Should().BeEmpty();
    }

    [Fact]
    public void ActionMetadata_Properties_CanBeSet()
    {
        ActionMetadata action = new()
        {
            Name = "Create",
            UiKey = "btn-create",
            Type = PermissionType.Action,
            IsGranted = true,
            Children = [new ActionMetadata { Name = "Sub", UiKey = "btn-sub" }]
        };

        action.Name.Should().Be("Create");
        action.UiKey.Should().Be("btn-create");
        action.Type.Should().Be(PermissionType.Action);
        action.IsGranted.Should().BeTrue();
        action.Children.Should().HaveCount(1);
    }

    [Fact]
    public void MenuMetadata_CanHaveMultipleActions()
    {
        MenuMetadata meta = new()
        {
            GroupName = "Admin",
            GroupDisplayName = "Administration",
            Actions =
            [
                new ActionMetadata { Name = "Create", IsGranted = true },
                new ActionMetadata { Name = "Delete", IsGranted = false }
            ]
        };

        meta.Actions.Should().HaveCount(2);
        meta.Actions[0].IsGranted.Should().BeTrue();
        meta.Actions[1].IsGranted.Should().BeFalse();
    }
}

public class SystemExecutionContextScopeTests
{
    [Fact]
    public void Scope_ThrowsArgumentNull_WhenAccessorIsNull()
    {
        SystemExecutionContext ctx = new(null, "u1", "name", "c1", null, null, true, [], "http");

        Action act = () => new SystemExecutionContextScope(null!, ctx);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Scope_ThrowsArgumentNull_WhenContextIsNull()
    {
        SystemExecutionContextAccessor accessor = new();

        Action act = () => new SystemExecutionContextScope(accessor, null!);

        act.Should().Throw<MArgumentException>();
    }

    [Fact]
    public void Scope_DoubleDispose_DoesNotThrow()
    {
        SystemExecutionContextAccessor accessor = new();
        SystemExecutionContext ctx = new(null, "u1", "name", "c1", null, null, true, [], "http");

        SystemExecutionContextScope scope = new(accessor, ctx);
        scope.Dispose();

        Action act = () => scope.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void NestedScopes_RestoreCorrectly()
    {
        SystemExecutionContextAccessor accessor = new();
        SystemExecutionContext ctx1 = new("t1", "u1", "name1", "c1", null, null, true, [], "http");
        SystemExecutionContext ctx2 = new("t2", "u2", "name2", "c2", null, null, true, [], "grpc");
        SystemExecutionContext ctx3 = new("t3", "u3", "name3", "c3", null, null, true, [], "amqp");

        using (new SystemExecutionContextScope(accessor, ctx1))
        {
            accessor.Get().TenantId.Should().Be("t1");

            using (new SystemExecutionContextScope(accessor, ctx2))
            {
                accessor.Get().TenantId.Should().Be("t2");

                using (new SystemExecutionContextScope(accessor, ctx3))
                {
                    accessor.Get().TenantId.Should().Be("t3");
                }

                accessor.Get().TenantId.Should().Be("t2");
            }

            accessor.Get().TenantId.Should().Be("t1");
        }
    }
}
