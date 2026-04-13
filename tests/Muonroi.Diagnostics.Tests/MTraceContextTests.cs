using FluentAssertions;
using Moq;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Serialization;
using Muonroi.Diagnostics.Context;
using Xunit;

namespace Muonroi.Diagnostics.Tests;

public class MTraceContextTests
{
    private readonly Mock<IMJsonSerializeService> _jsonMock = new();
    private readonly MTraceContext _sut;

    public MTraceContextTests()
    {
        _jsonMock.Setup(x => x.Serialize(It.IsAny<object>())).Returns("{}");
        _sut = new MTraceContext(_jsonMock.Object);
    }

    [Fact]
    public void Current_Without_Scope_Should_Be_Null()
    {
        _sut.Current.Should().BeNull();
    }

    [Fact]
    public void Begin_Should_Create_Active_Session()
    {
        using IDisposable scope = _sut.Begin("session-1", "tenant-1", "user-1");

        _sut.Current.Should().NotBeNull();
        _sut.Current!.SessionId.Should().Be("session-1");
        _sut.Current!.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Begin_With_LineTrace_Enabled_Should_Set_Flag()
    {
        using IDisposable scope = _sut.Begin("session-1", "tenant-1", "user-1", lineTraceEnabled: true);

        _sut.Current.Should().NotBeNull();
        _sut.Current!.IsLineTraceEnabled.Should().BeTrue();
    }

    [Fact]
    public void Dispose_Scope_Should_Restore_Previous_Session()
    {
        IDisposable scope = _sut.Begin("session-1", "tenant-1", "user-1");
        scope.Dispose();

        _sut.Current.Should().BeNull();
    }

    [Fact]
    public void Nested_Scopes_Should_Restore_Correctly()
    {
        using IDisposable outer = _sut.Begin("outer", "t", "u");
        ITraceSession? outerSession = _sut.Current;

        IDisposable inner = _sut.Begin("inner", "t", "u");
        _sut.Current!.SessionId.Should().Be("inner");

        inner.Dispose();
        _sut.Current.Should().BeSameAs(outerSession);
    }

    [Fact]
    public void Begin_With_Null_TenantId_Should_Work()
    {
        using IDisposable scope = _sut.Begin("s1", null, null);

        _sut.Current.Should().NotBeNull();
        _sut.Current!.TenantId.Should().BeNull();
        _sut.Current!.UserId.Should().BeNull();
    }
}

public class MTraceSessionTests
{
    private readonly Mock<IMJsonSerializeService> _jsonMock = new();
    private readonly MTraceContext _ctx;

    public MTraceSessionTests()
    {
        _jsonMock.Setup(x => x.Serialize(It.IsAny<object>())).Returns<object>(o => $"serialized:{o}");
        _ctx = new MTraceContext(_jsonMock.Object);
    }

    [Fact]
    public void BeginNode_Should_Create_Root_Node()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using IDisposable node = session.BeginNode("TestNode", MTraceNodeType.MediatorRequest);

        MTraceSessionRecord export = session.Export();
        export.Nodes.Should().HaveCount(1);
        export.Nodes[0].Name.Should().Be("TestNode");
        export.Nodes[0].Type.Should().Be(MTraceNodeType.MediatorRequest);
    }

    [Fact]
    public void Nested_BeginNode_Should_Create_Child_Node()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable parent = session.BeginNode("Parent", MTraceNodeType.MediatorRequest))
        {
            using (IDisposable child = session.BeginNode("Child", MTraceNodeType.Rule))
            {
            }
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes.Should().HaveCount(1);
        export.Nodes[0].Children.Should().HaveCount(1);
        export.Nodes[0].Children[0].Name.Should().Be("Child");
    }

    [Fact]
    public void Record_Should_Add_Event_To_Current_Node()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Node", MTraceNodeType.Rule))
        {
            session.Record("test message", new { key = "value" });
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].Events.Should().ContainSingle(e => e.Message == "test message");
    }

    [Fact]
    public void Record_Without_Node_Should_Not_Throw()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        Action act = () => session.Record("no node");
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordFactSnapshot_Should_Set_InputFacts_For_Before_Phase()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Rule", MTraceNodeType.Rule))
        {
            Dictionary<string, object?> facts = new() { ["amount"] = 100 };
            session.RecordFactSnapshot("before", facts);
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].InputFactsJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RecordFactSnapshot_Should_Set_OutputFacts_For_After_Phase()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Rule", MTraceNodeType.Rule))
        {
            Dictionary<string, object?> facts = new() { ["result"] = "ok" };
            session.RecordFactSnapshot("after", facts);
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].OutputFactsJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void RecordFactSnapshot_Should_Ignore_NonRule_Node()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("NotRule", MTraceNodeType.MediatorRequest))
        {
            Dictionary<string, object?> facts = new() { ["x"] = 1 };
            session.RecordFactSnapshot("before", facts);
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].InputFactsJson.Should().BeNull();
    }

    [Fact]
    public void RecordLineTrace_Should_Add_When_LineTrace_Enabled()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1", lineTraceEnabled: true);
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Rule", MTraceNodeType.Rule))
        {
            session.RecordLineTrace(42, "myVar", "hello");
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].LineTraces.Should().ContainSingle(lt => lt.Line == 42 && lt.Variable == "myVar");
    }

    [Fact]
    public void RecordLineTrace_Should_Not_Add_When_LineTrace_Disabled()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1", lineTraceEnabled: false);
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Rule", MTraceNodeType.Rule))
        {
            session.RecordLineTrace(42, "myVar", "hello");
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].LineTraces.Should().BeEmpty();
    }

    [Fact]
    public void RecordBranchTrace_Should_Add_When_LineTrace_Enabled()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1", lineTraceEnabled: true);
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Rule", MTraceNodeType.Rule))
        {
            session.RecordBranchTrace(10, "x > 5", true);
        }

        MTraceSessionRecord export = session.Export();
        MLineTraceRecord trace = export.Nodes[0].LineTraces[0];
        trace.IsBranch.Should().BeTrue();
        trace.Condition.Should().Be("x > 5");
        trace.BranchTaken.Should().BeTrue();
    }

    [Fact]
    public void MarkFailed_Should_Set_Error_On_Current_Node()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Rule", MTraceNodeType.Rule))
        {
            session.MarkFailed("something broke", new InvalidOperationException("test"));
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].HasError.Should().BeTrue();
        export.Nodes[0].ErrorReason.Should().Be("something broke");
        export.HasErrors.Should().BeTrue();
    }

    [Fact]
    public void Export_Should_Capture_Session_Metadata()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        MTraceSessionRecord export = session.Export();
        export.SessionId.Should().Be("s1");
        export.TenantId.Should().Be("t1");
        export.UserId.Should().Be("u1");
        export.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void EndNode_Should_Set_Duration()
    {
        using IDisposable scope = _ctx.Begin("s1", "t1", "u1");
        ITraceSession session = _ctx.Current!;

        using (IDisposable node = session.BeginNode("Node", MTraceNodeType.Rule))
        {
            Thread.Sleep(10);
        }

        MTraceSessionRecord export = session.Export();
        export.Nodes[0].DurationMs.Should().BeGreaterThan(0);
    }
}
