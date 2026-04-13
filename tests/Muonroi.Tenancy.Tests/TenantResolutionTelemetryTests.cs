using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Muonroi.Core.Abstractions.Constants;
using Xunit;

namespace Muonroi.Tenancy.Tests;

/// <summary>
/// Tests for OpenTelemetry counter metrics emitted on tenant auth failures.
/// Uses System.Diagnostics.Metrics.MeterListener — built-in .NET 8 test utility.
/// </summary>
public class TenantResolutionTelemetryTests
{
    [Fact]
    public void RecordAuthFailure_Header_Mismatch_Increments_Counter_With_Tags()
    {
        // Arrange
        using MeterListener listener = new();
        long capturedValue = 0;
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TenantResolutionTelemetry.MeterName &&
                instrument.Name == TenantResolutionTelemetry.AuthFailureCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            capturedValue += measurement;
            capturedTags = tags.ToArray();
        });

        listener.Start();

        // Act
        TenantResolutionTelemetry.RecordAuthFailure("header_claim_mismatch", "tenant-a", "tenant-b");
        listener.RecordObservableInstruments();

        // Assert
        Assert.Equal(1, capturedValue);
        Assert.NotNull(capturedTags);
        Assert.Contains(capturedTags, t => t.Key == "failure_reason" && (string?)t.Value == "header_claim_mismatch");
        Assert.Contains(capturedTags, t => t.Key == "header_tenant_id" && (string?)t.Value == "tenant-a");
        Assert.Contains(capturedTags, t => t.Key == "claim_tenant_id" && (string?)t.Value == "tenant-b");
    }

    [Fact]
    public void RecordAuthFailure_Missing_Claim_Increments_Counter_With_Missing_Claim_Reason()
    {
        // Arrange
        using MeterListener listener = new();
        long capturedValue = 0;
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TenantResolutionTelemetry.MeterName &&
                instrument.Name == TenantResolutionTelemetry.AuthFailureCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            capturedValue += measurement;
            capturedTags = tags.ToArray();
        });

        listener.Start();

        // Act
        TenantResolutionTelemetry.RecordAuthFailure("missing_claim", null, null);
        listener.RecordObservableInstruments();

        // Assert
        Assert.Equal(1, capturedValue);
        Assert.NotNull(capturedTags);
        Assert.Contains(capturedTags, t => t.Key == "failure_reason" && (string?)t.Value == "missing_claim");
    }

    [Fact]
    public async Task Middleware_Header_Mismatch_Emits_Counter_With_Correct_Tags()
    {
        // Arrange
        using MeterListener listener = new();
        long capturedValue = 0;
        KeyValuePair<string, object?>[]? capturedTags = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TenantResolutionTelemetry.MeterName &&
                instrument.Name == TenantResolutionTelemetry.AuthFailureCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            capturedValue += measurement;
            capturedTags = tags.ToArray();
        });

        listener.Start();

        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-a");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-b")
        ], "test"));

        TenantResolutionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.Invoke(context);
        listener.RecordObservableInstruments();

        // Assert: 401 returned (behavior unchanged)
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);

        // Assert: counter incremented with correct tags
        Assert.Equal(1, capturedValue);
        Assert.NotNull(capturedTags);
        Assert.Contains(capturedTags, t => t.Key == "failure_reason" && (string?)t.Value == "header_claim_mismatch");
        Assert.Contains(capturedTags, t => t.Key == "header_tenant_id" && (string?)t.Value == "tenant-a");
        Assert.Contains(capturedTags, t => t.Key == "claim_tenant_id" && (string?)t.Value == "tenant-b");
    }

    [Fact]
    public async Task Middleware_Successful_Resolution_Does_Not_Increment_Counter()
    {
        // Arrange
        using MeterListener listener = new();
        long capturedValue = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == TenantResolutionTelemetry.MeterName &&
                instrument.Name == TenantResolutionTelemetry.AuthFailureCounterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            capturedValue += measurement;
        });

        listener.Start();

        DefaultHttpContext context = new();
        context.Request.Headers.Append(CustomHeader.TenantId, "tenant-a");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimConstants.TenantId, "tenant-a")
        ], "test"));

        TenantResolutionMiddleware middleware = new(_ => Task.CompletedTask);

        // Act
        await middleware.Invoke(context);
        listener.RecordObservableInstruments();

        // Assert: no failure counter incremented on success
        Assert.Equal(0, capturedValue);
    }
}
