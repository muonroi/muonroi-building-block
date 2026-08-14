namespace Muonroi.Data.Dapper.Tests.Rls;

/// <summary>
/// Tests for <see cref="DapperRlsBypass"/> and <see cref="IBypassScope"/> covering the
/// AsyncLocal ambient scope state transitions (synchronous and across async continuations).
/// </summary>
public sealed class BypassScopeTests
{
    [Fact]
    public void IsActive_BeforeEnter_IsFalse()
    {
        DapperRlsBypass.IsActive.Should().BeFalse("bypass is never the default");
    }

    [Fact]
    public void IsActive_InsideScope_IsTrue()
    {
        using (DapperRlsBypass.Enter())
        {
            DapperRlsBypass.IsActive.Should().BeTrue("the scope activates the ambient bypass flag");
        }
    }

    [Fact]
    public void IsActive_AfterScopeDisposed_IsFalse()
    {
        using (DapperRlsBypass.Enter())
        {
            // scope active here
        }

        DapperRlsBypass.IsActive.Should().BeFalse("disposing the scope resets the ambient flag");
    }

    [Fact]
    public void Enter_ReturnsIBypassScope_WhichIsDisposable()
    {
        IBypassScope scope = DapperRlsBypass.Enter();
        try
        {
            scope.Should().BeAssignableTo<IDisposable>("IBypassScope extends IDisposable");
        }
        finally
        {
            scope.Dispose();
        }
    }

    [Fact]
    public async Task IsActive_FlowsAcrossAsyncContinuation()
    {
        using (DapperRlsBypass.Enter())
        {
            DapperRlsBypass.IsActive.Should().BeTrue("active before await");
            await Task.Yield();
            DapperRlsBypass.IsActive.Should().BeTrue("AsyncLocal flows into the continuation after await");
        }

        DapperRlsBypass.IsActive.Should().BeFalse("flag resets after scope disposed");
    }

    [Fact]
    public async Task Dispose_DoesNotAffectAlreadyCapturedChildContext()
    {
        // A fire-and-forget child captures the AsyncLocal context at creation time.
        // Disposing the parent scope must not flip the child's captured copy (copy-on-capture).
        TaskCompletionSource childObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource releaseChild = new(TaskCreationOptions.RunContinuationsAsynchronously);
        bool childSawActive = false;

        Task child;
        using (DapperRlsBypass.Enter())
        {
            child = Task.Run(async () =>
            {
                childSawActive = DapperRlsBypass.IsActive;
                childObserved.SetResult();
                await releaseChild.Task;
                // Even after the parent disposed, the child's captured context is unaffected.
                childSawActive = childSawActive && DapperRlsBypass.IsActive;
            });

            await childObserved.Task;
        }

        // Parent scope disposed here; now release the child.
        releaseChild.SetResult();
        await child;

        childSawActive.Should().BeTrue(
            "AsyncLocal uses copy-on-capture; the parent's Dispose does not mutate the child's captured copy");
    }
}
