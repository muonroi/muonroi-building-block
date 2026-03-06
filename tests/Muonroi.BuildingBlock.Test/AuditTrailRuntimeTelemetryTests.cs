namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class AuditTrailRuntimeTelemetryTests
{
    private static readonly LicenseState AuditTrailLicensed = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.AuditTrail]
    };

    [Fact]
    public void FileStore_AppendAndRead_EmitActivityAndMetrics()
    {
        string path = Path.Combine(Path.GetTempPath(), "muonroi_audit_chain_tests", $"{Guid.NewGuid():N}.log");

        try
        {
            LicenseConfigs configs = new()
            {
                ChainStorage = LicenseChainStorage.File,
                ChainFilePath = path,
                EnforcementMode = LicenseEnforcementMode.Development,
                ProjectSeed = "1234567890123456",
                FingerprintSalt = "tests"
            };
            FileFingerprintChainStore store = new(null, configs);

            List<Activity> stoppedActivities = [];
            using ActivityListener activityListener = new();
            activityListener.ShouldListenTo = source => source.Name == AuditTrailRuntimeTelemetry.ActivitySourceName;
            activityListener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            activityListener.ActivityStopped = activity => stoppedActivities.Add(activity);
            ActivitySource.AddActivityListener(activityListener);

            HashSet<string> meteredOperations = new(StringComparer.OrdinalIgnoreCase);
            bool sawTenantTag = false;
            using MeterListener meterListener = new();
            meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AuditTrailRuntimeTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            {
                if (!string.Equals(instrument.Name, "audittrail_operations_total", StringComparison.Ordinal))
                    return;

                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (string.Equals(tag.Key, "audittrail.operation", StringComparison.Ordinal) &&
                        tag.Value is string operation &&
                        !string.IsNullOrWhiteSpace(operation))
                    {
                        meteredOperations.Add(operation);
                    }

                    if (string.Equals(tag.Key, "tenant.id", StringComparison.Ordinal) &&
                        string.Equals(tag.Value?.ToString(), "tenant-a", StringComparison.Ordinal))
                    {
                        sawTenantTag = true;
                    }
                }
            });
            meterListener.Start();

            FingerprintChainEntry entry = new()
            {
                Sequence = 1,
                Timestamp = DateTimeOffset.UtcNow,
                TenantId = "tenant-a",
                ActionType = "api.request",
                Signature = "sig-a"
            };
            store.Append(entry);
            List<FingerprintChainEntry> entries = [.. store.GetRecentEntries(10, tenantId: "tenant-a")];

            Assert.Single(entries);
            Assert.Equal("tenant-a", entries[0].TenantId);

            Assert.Contains(stoppedActivities, activity =>
                string.Equals(activity.OperationName, "audit-trail.store_append", StringComparison.Ordinal));
            Assert.Contains(stoppedActivities, activity =>
                string.Equals(activity.OperationName, "audit-trail.store_read", StringComparison.Ordinal));

            Assert.Contains("store_append", meteredOperations);
            Assert.Contains("store_read", meteredOperations);
            Assert.True(sawTenantTag);
        }
        finally
        {
            SafeDelete(path);
        }
    }

    [Fact]
    public async Task Submitter_Submit_EmitsMetrics_WithSubmitOperation()
    {
        TestHandler handler = new((_, _) =>
        {
            HttpResponseMessage message = new(HttpStatusCode.OK);
            ChainSubmissionResponse value = new()
            {
                Accepted = true,
                NewNonce = "nonce"
            };
            message.Content = System.Net.Http.Json.JsonContent.Create(value);
            return message;
        });

        HashSet<string> meteredOperations = new(StringComparer.OrdinalIgnoreCase);
        using MeterListener meterListener = new();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == AuditTrailRuntimeTelemetry.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (!string.Equals(instrument.Name, "audittrail_operations_total", StringComparison.Ordinal))
                return;

            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (string.Equals(tag.Key, "audittrail.operation", StringComparison.Ordinal) &&
                    tag.Value is string operation &&
                    !string.IsNullOrWhiteSpace(operation))
                {
                    meteredOperations.Add(operation);
                }
            }
        });
        meterListener.Start();

        LicenseConfigs configs = new()
        {
            ChainStorage = LicenseChainStorage.File,
            Online = new OnlineLicenseConfigs
            {
                Endpoint = "https://license.muonroi.com",
                ChainSubmissionEndpoint = "/api/v1/chain/submit"
            }
        };
        ChainSubmitter submitter = new(
            configs,
            new TestHttpClientFactory(handler, "https://license.muonroi.com"),
            licenseState: AuditTrailLicensed);

        FingerprintChainEntry entry = new()
        {
            Sequence = 1,
            TenantId = "tenant-a",
            ActionType = "api.request",
            Signature = "sig"
        };
        ChainSubmissionResponse response = await submitter.SubmitAsync(
            [entry],
            "tenant-a");

        Assert.True(response.Accepted);
        Assert.Contains("submit_chain", meteredOperations);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            string? folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) &&
                !Directory.EnumerateFileSystemEntries(folder).Any())
            {
                Directory.Delete(folder);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private sealed class TestHttpClientFactory(TestHandler handler, string baseAddress) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            HttpClient client = new(handler)
            {
                BaseAddress = new Uri(baseAddress)
            };
            return client;
        }
    }

    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request, cancellationToken));
        }
    }
}
