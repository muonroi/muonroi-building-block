using System.Diagnostics;
using Muonroi.Core.Abstractions.Diagnostics;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Core.Abstractions.Tests.Diagnostics;

public class MCausalChainTests
{
    // ─── Serialization / Deserialization ───────────────────────────────────────

    [Fact]
    public void Serialize_RoundTrip_PreservesAllFields()
    {
        var chain = new MCausalChain
        {
            OriginService = "ServiceA",
            OriginErrorCode = "MBB:SVC:001",
            OriginLayer = MExceptionLayer.Domain,
            OriginMessage = "Something went wrong",
            Timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };

        string base64 = MCausalChain.Serialize(chain);
        MCausalChain? result = MCausalChain.TryDeserialize(base64);

        result.Should().NotBeNull();
        result!.OriginService.Should().Be("ServiceA");
        result.OriginErrorCode.Should().Be("MBB:SVC:001");
        result.OriginLayer.Should().Be(MExceptionLayer.Domain);
        result.OriginMessage.Should().Be("Something went wrong");
        result.Timestamp.Should().Be(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Serialize_WithNestedCause_PreservesNesting()
    {
        var inner = new MCausalChain
        {
            OriginService = "ServiceC",
            OriginErrorCode = "MBB:C:001",
            OriginLayer = MExceptionLayer.Infrastructure,
            Timestamp = DateTimeOffset.UtcNow
        };
        var middle = new MCausalChain
        {
            OriginService = "ServiceB",
            OriginErrorCode = "MBB:B:001",
            OriginLayer = MExceptionLayer.Application,
            Cause = inner,
            Timestamp = DateTimeOffset.UtcNow
        };
        var outer = new MCausalChain
        {
            OriginService = "ServiceA",
            OriginErrorCode = "MBB:A:001",
            OriginLayer = MExceptionLayer.Presentation,
            Cause = middle,
            Timestamp = DateTimeOffset.UtcNow
        };

        string base64 = MCausalChain.Serialize(outer);
        MCausalChain? result = MCausalChain.TryDeserialize(base64);

        result.Should().NotBeNull();
        result!.OriginService.Should().Be("ServiceA");
        result.Cause.Should().NotBeNull();
        result.Cause!.OriginService.Should().Be("ServiceB");
        result.Cause.Cause.Should().NotBeNull();
        result.Cause.Cause!.OriginService.Should().Be("ServiceC");
    }

    [Fact]
    public void Serialize_NullMessage_RoundTripPreservesNull()
    {
        var chain = new MCausalChain
        {
            OriginService = "ServiceA",
            OriginErrorCode = "MBB:SVC:001",
            OriginLayer = MExceptionLayer.Domain,
            OriginMessage = null,
            Timestamp = DateTimeOffset.UtcNow
        };

        string base64 = MCausalChain.Serialize(chain);
        MCausalChain? result = MCausalChain.TryDeserialize(base64);

        result.Should().NotBeNull();
        result!.OriginMessage.Should().BeNull();
    }

    // ─── Depth enforcement ─────────────────────────────────────────────────────

    [Fact]
    public void Depth_WithinLimit_DeserializesSuccessfully()
    {
        var chain = BuildChainOfDepth(3);
        chain.Depth().Should().Be(3);

        string base64 = MCausalChain.Serialize(chain);
        MCausalChain? result = MCausalChain.TryDeserialize(base64, maxDepth: 10);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Depth_ExactLimit_DeserializesSuccessfully()
    {
        var chain = BuildChainOfDepth(10);
        chain.Depth().Should().Be(10);

        string base64 = MCausalChain.Serialize(chain);
        MCausalChain? result = MCausalChain.TryDeserialize(base64, maxDepth: 10);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Depth_ExceedsLimit_ReturnsNull()
    {
        var chain = BuildChainOfDepth(11);
        chain.Depth().Should().Be(11);

        string base64 = MCausalChain.Serialize(chain);
        // Per D-03: reject (return null), do not truncate
        MCausalChain? result = MCausalChain.TryDeserialize(base64, maxDepth: 10);

        result.Should().BeNull();
    }

    // ─── Invalid input handling ────────────────────────────────────────────────

    [Fact]
    public void TryDeserialize_InvalidBase64_ReturnsNull()
    {
        MCausalChain? result = MCausalChain.TryDeserialize("not-valid-base64!!!");

        result.Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_InvalidJson_ReturnsNull()
    {
        string invalidJson = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{ invalid json }"));
        MCausalChain? result = MCausalChain.TryDeserialize(invalidJson);

        result.Should().BeNull();
    }

    // ─── MException integration ────────────────────────────────────────────────

    [Fact]
    public void MException_CausalChain_PopulatedFromActivityBaggage()
    {
        var chain = new MCausalChain
        {
            OriginService = "UpstreamService",
            OriginErrorCode = "MBB:UP:001",
            OriginLayer = MExceptionLayer.Infrastructure,
            Timestamp = DateTimeOffset.UtcNow
        };
        string serialized = MCausalChain.Serialize(chain);

        using var activity = new Activity("test-operation").Start();
        activity.SetBaggage("muonroi.causal", serialized);

        var ex = new TestException("Test error");

        ex.CausalChain.Should().NotBeNull();
        ex.CausalChain!.OriginService.Should().Be("UpstreamService");
        ex.CausalChain.OriginErrorCode.Should().Be("MBB:UP:001");
    }

    [Fact]
    public void MException_CausalChain_NullWhenNoBaggage()
    {
        using var activity = new Activity("test-operation-no-baggage").Start();
        // No SetBaggage call

        var ex = new TestException("Test error");

        ex.CausalChain.Should().BeNull();
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static MCausalChain BuildChainOfDepth(int depth)
    {
        MCausalChain? current = null;
        for (int i = depth; i >= 1; i--)
        {
            current = new MCausalChain
            {
                OriginService = $"Service{i}",
                OriginErrorCode = $"MBB:SVC:{i:D3}",
                OriginLayer = MExceptionLayer.Domain,
                Cause = current,
                Timestamp = DateTimeOffset.UtcNow
            };
        }
        return current!;
    }

    private sealed class TestException(string message) : MException(
        errorCode: "TEST:001",
        message: message,
        category: MExceptionCategory.Domain,
        httpStatusCode: 500)
    { }
}
