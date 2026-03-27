using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Interfaces;
using Muonroi.Core.Abstractions.Serialization;
using System.Diagnostics;

namespace Muonroi.Diagnostics.Context;

internal sealed class MTraceSession : ITraceSession
{
    private readonly IMJsonSerializeService _json;
    private readonly Stack<MTraceNodeRecord> _nodeStack = new();
    private readonly List<MTraceNodeRecord> _rootNodes = new();
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public string SessionId { get; }
    public string? TenantId { get; }
    public string? UserId { get; }
    public bool IsActive => true;
    public bool IsLineTraceEnabled { get; }
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public MTraceSession(string sessionId, string? tenantId, string? userId, bool lineTraceEnabled, IMJsonSerializeService json)
    {
        SessionId = sessionId;
        TenantId = tenantId;
        UserId = userId;
        IsLineTraceEnabled = lineTraceEnabled;
        _json = json;
    }

    public IDisposable BeginNode(string name, MTraceNodeType type)
    {
        var node = new MTraceNodeRecord
        {
            NodeId = Guid.NewGuid().ToString("N"),
            Name = name,
            Type = type,
            StartedAt = DateTime.UtcNow
        };

        if (_nodeStack.TryPeek(out var parent))
        {
            parent.Children.Add(node);
        }
        else
        {
            _rootNodes.Add(node);
        }

        _nodeStack.Push(node);
        return new NodeScope(this, _sw.ElapsedMilliseconds);
    }

    public void Record(string message, object? payload = null)
    {
        if (_nodeStack.TryPeek(out var node))
        {
            node.Events.Add(new MTraceEventRecord
            {
                Message = message,
                PayloadJson = payload != null ? _json.Serialize(payload) : null,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public void RecordFactSnapshot(string phase, IReadOnlyDictionary<string, object?> facts)
    {
        if (_nodeStack.TryPeek(out var node) && node.Type == MTraceNodeType.Rule)
        {
            var json = _json.Serialize(facts);
            if (phase == "before") node.InputFactsJson = json;
            else node.OutputFactsJson = json;
        }
    }

    public void RecordLineTrace(int line, string variable, object? value, string? sourceMember = null)
    {
        if (IsLineTraceEnabled && _nodeStack.TryPeek(out var node))
        {
            node.LineTraces.Add(new MLineTraceRecord
            {
                Line = line,
                Variable = variable,
                ValueJson = _json.Serialize(value)
            });
        }
    }

    public void RecordBranchTrace(int line, string condition, bool taken)
    {
        if (IsLineTraceEnabled && _nodeStack.TryPeek(out var node))
        {
            node.LineTraces.Add(new MLineTraceRecord
            {
                Line = line,
                IsBranch = true,
                Condition = condition,
                BranchTaken = taken
            });
        }
    }

    public void MarkFailed(string reason, Exception? ex = null)
    {
        if (_nodeStack.TryPeek(out var node))
        {
            node.HasError = true;
            node.ErrorReason = reason;
            if (ex != null)
            {
                Record($"Exception: {ex.GetType().Name}", ex);
            }
        }
    }

    public MTraceSessionRecord Export()
    {
        return new MTraceSessionRecord
        {
            SessionId = SessionId,
            TenantId = TenantId,
            UserId = UserId,
            StartedAt = StartedAt,
            DurationMs = _sw.Elapsed.TotalMilliseconds,
            HasErrors = _rootNodes.Any(n => n.HasError || HasAnyError(n)),
            Nodes = _rootNodes
        };
    }

    private bool HasAnyError(MTraceNodeRecord node) => node.HasError || node.Children.Any(HasAnyError);

    private void EndNode(double startMs)
    {
        if (_nodeStack.TryPop(out var node))
        {
            node.DurationMs = _sw.Elapsed.TotalMilliseconds - startMs;
        }
    }

    private sealed class NodeScope(MTraceSession session, double startMs) : IDisposable
    {
        public void Dispose() => session.EndNode(startMs);
    }
}
