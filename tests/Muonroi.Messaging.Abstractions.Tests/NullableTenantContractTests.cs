using Muonroi.Messaging.Abstractions.Contracts;
using Muonroi.Tenancy.Abstractions;

namespace Muonroi.Messaging.Abstractions.Tests;

/// <summary>
/// Contract tests proving that null TenantId is valid on messaging interfaces (DCP-09).
/// </summary>
public class NullableTenantContractTests
{
    /// <summary>
    /// Verifies that an IRoutingContext implementation accepts null TenantId without throwing.
    /// </summary>
    [Fact]
    public void IRoutingContext_NullTenantId_IsValid()
    {
        StubRoutingContext context = new(tenantId: null, correlationId: "corr-1", messageType: "Test");

        context.TenantId.Should().BeNull();
        context.CorrelationId.Should().Be("corr-1");
    }

    /// <summary>
    /// Verifies that an IRoutingContext implementation with non-null TenantId still works (DCP-10 regression).
    /// </summary>
    [Fact]
    public void IRoutingContext_NonNullTenantId_StillWorks()
    {
        StubRoutingContext context = new(tenantId: "tenant-a", correlationId: "corr-2", messageType: "Test");

        context.TenantId.Should().Be("tenant-a");
    }

    /// <summary>
    /// Verifies that an IMuonroiSaga implementation accepts null TenantId without throwing.
    /// </summary>
    [Fact]
    public void IMuonroiSaga_NullTenantId_IsValid()
    {
        StubSaga saga = new() { TenantId = null };

        saga.TenantId.Should().BeNull();
        ((ITenantScoped)saga).TenantId.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an IMuonroiSaga implementation with non-null TenantId still works (DCP-10 regression).
    /// </summary>
    [Fact]
    public void IMuonroiSaga_NonNullTenantId_StillWorks()
    {
        StubSaga saga = new() { TenantId = "tenant-b" };

        saga.TenantId.Should().Be("tenant-b");
    }

    /// <summary>
    /// Stub implementation of IRoutingContext for contract testing.
    /// </summary>
    private sealed class StubRoutingContext(string? tenantId, string correlationId, string messageType) : IRoutingContext
    {
        /// <inheritdoc />
        public string? TenantId { get; } = tenantId;

        /// <inheritdoc />
        public string CorrelationId { get; } = correlationId;

        /// <inheritdoc />
        public string MessageType { get; } = messageType;

        /// <inheritdoc />
        public IReadOnlyDictionary<string, string> Headers { get; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Stub implementation of IMuonroiSaga for contract testing.
    /// </summary>
    private sealed class StubSaga : IMuonroiSaga
    {
        /// <inheritdoc />
        public Guid CorrelationId { get; set; }

        /// <inheritdoc />
        public string? TenantId { get; set; }

        /// <inheritdoc />
        public DateTime CreationTime { get; set; }

        /// <inheritdoc />
        public DateTime? LastModificationTime { get; set; }
    }
}
