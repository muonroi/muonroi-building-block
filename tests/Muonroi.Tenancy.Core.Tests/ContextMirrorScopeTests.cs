namespace Muonroi.Tenancy.Core.Tests;

public class ContextMirrorScopeTests
{
    private sealed class FakeLogScopeFactory : ILogScopeFactory
    {
        public IReadOnlyDictionary<string, object?>? LastState { get; private set; }
        public bool Disposed { get; private set; }

        public IDisposable BeginScope(IReadOnlyDictionary<string, object?> state)
        {
            LastState = new Dictionary<string, object?>(state);
            return new Scope(() => Disposed = true);
        }

        private sealed class Scope(Action onDispose) : IDisposable
        {
            public void Dispose()
            {
                onDispose();
            }
        }
    }

    [Fact]
    public void Apply_Mirrors_Context_And_Restores_On_Dispose()
    {
        FakeLogScopeFactory logScopeFactory = new();
        SystemExecutionContext context = new("tenant-a", "user-1", "admin", "corr-1", null, null, true, ["read"], "http");
        TenantContext.CurrentTenantId = "previous-tenant";
        UserContext.CurrentUserGuid = "previous-user";
        UserContext.CurrentUsername = "previous-name";

        using (ContextMirrorScope.Apply(context, logScopeFactory))
        {
            TenantContext.CurrentTenantId.Should().Be("tenant-a");
            UserContext.CurrentUserGuid.Should().Be("user-1");
            UserContext.CurrentUsername.Should().Be("admin");
            logScopeFactory.LastState.Should().NotBeNull();
            logScopeFactory.LastState!["TenantId"].Should().Be("tenant-a");
        }

        TenantContext.CurrentTenantId.Should().Be("previous-tenant");
        UserContext.CurrentUserGuid.Should().Be("previous-user");
        UserContext.CurrentUsername.Should().Be("previous-name");
        logScopeFactory.Disposed.Should().BeTrue();
    }
}
