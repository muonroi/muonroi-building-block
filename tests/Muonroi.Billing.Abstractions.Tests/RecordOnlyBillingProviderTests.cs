namespace Muonroi.Billing.Abstractions.Tests;

/// <summary>
/// Behavior tests for <see cref="RecordOnlyBillingProvider"/> (MON-02): records events with no
/// external call, logs-then-swallows on sink failure (No Silent Catch), and previews compute-only.
/// </summary>
public sealed class RecordOnlyBillingProviderTests
{
    private static BillableEvent SampleEvent()
        => new("tenant-1", QuotaType.PdfRendersPerDay, 3, DateTimeOffset.UnixEpoch);

    // Test 1: RecordAsync persists the event into the recorded list, zero external calls.
    [Fact]
    public async Task RecordAsync_RecordsEvent_AndMakesNoExternalCall()
    {
        var provider = new RecordOnlyBillingProvider();

        await provider.RecordAsync(SampleEvent());

        provider.RecordedEvents.Should().ContainSingle()
            .Which.TenantId.Should().Be("tenant-1");
    }

    // Test 2: When the recording sink throws, RecordAsync logs via IMLog.Error and does NOT rethrow.
    [Fact]
    public async Task RecordAsync_WhenSinkThrows_LogsErrorAndDoesNotRethrow()
    {
        var logger = new RecordingLogger();
        var provider = new RecordOnlyBillingProvider(
            logger,
            sink: _ => throw new InvalidTimeZoneException("sink boom"));

        Func<Task> act = () => provider.RecordAsync(SampleEvent());

        await act.Should().NotThrowAsync();
        logger.ErrorCount.Should().Be(1);
        logger.LastException.Should().BeOfType<InvalidTimeZoneException>();
    }

    // Test 3: PreviewInvoiceAsync returns the supplied line items unchanged, no charge, no external call.
    [Fact]
    public async Task PreviewInvoiceAsync_ReturnsSuppliedLineItems_Unchanged()
    {
        var provider = new RecordOnlyBillingProvider();
        var items = new List<UsageLineItem>
        {
            UsageLineItem.Create(QuotaType.PdfRendersPerDay, 10, 0.5m, "renders"),
        };

        IReadOnlyList<UsageLineItem> result = await provider.PreviewInvoiceAsync("tenant-1", items);

        result.Should().BeSameAs(items);
    }

    /// <summary>A fake <see cref="IMLog{T}"/> that records Error invocations for assertion.</summary>
    private sealed class RecordingLogger : IMLog<RecordOnlyBillingProvider>
    {
        public int ErrorCount { get; private set; }

        public Exception? LastException { get; private set; }

        public void Error(Exception? ex, string messageTemplate, params object?[] args)
        {
            ErrorCount++;
            LastException = ex;
        }

        public void InfoContext(string messageTemplate, params object?[] args) { }

        public void InfoContext(string messageTemplate, object? arg0 = null, object? arg1 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }

        public void ErrorContext(Exception? ex, string messageTemplate, params object?[] args)
        {
            ErrorCount++;
            LastException = ex;
        }

        public void ErrorContext(Exception? ex, string messageTemplate, object? arg0 = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0)
        {
            ErrorCount++;
            LastException = ex;
        }

        public void Audit(string messageTemplate, params object?[] args) { }

        public void Audit(string messageTemplate, string? auditType = null, string? action = null, bool isSuccess = true, string? targetId = null, string? targetType = null, object? metadata = null, string memberName = "", string sourceFilePath = "", int sourceLineNumber = 0) { }

        public IMLogContextScope BeginProperty(string key, object? value) => throw new NotSupportedException();

        public void Info(string messageTemplate, params object?[] args) { }

        public void Warn(string messageTemplate, params object?[] args) { }

        public void Debug(string messageTemplate, params object?[] args) { }

        public void InfoTrace(string messageTemplate, params object?[] args) { }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        { }
    }
}
