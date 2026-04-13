using Muonroi.Core.Abstractions.Exceptions;
namespace Muonroi.BuildingBlock.Test;

[Collection("NonParallel")]
public class GrpcServerInterceptorTests
{
    private static readonly LicenseState GrpcLicensedState = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.Grpc]
    };

    private static readonly LicenseState GrpcAndMultiTenantLicensedState = new()
    {
        IsValid = true,
        Tier = LicenseTier.Licensed,
        Features = [FreeTierFeatures.Premium.Grpc, FreeTierFeatures.Premium.MultiTenant]
    };

    private sealed class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _request;
        private readonly Metadata _trailers = [];
        private readonly IDictionary<object, object> _userState = new Dictionary<object, object>();

        public TestServerCallContext(Metadata? headers = null, HttpContext? httpContext = null)
        {
            _request = headers ?? [];
            if (httpContext != null) _userState["__HttpContext"] = httpContext;
        }

        protected override string MethodCore => "test";
        protected override string HostCore => "localhost";
        protected override string PeerCore => "peer";
        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);
        protected override Metadata RequestHeadersCore => _request;
        protected override CancellationToken CancellationTokenCore => CancellationToken.None;
        protected override Metadata ResponseTrailersCore => _trailers;
        protected override Grpc.Core.Status StatusCore { get; set; }
        protected override Grpc.Core.WriteOptions? WriteOptionsCore { get; set; }
        protected override AuthContext AuthContextCore { get; } = new("test", []);
        protected override IDictionary<object, object> UserStateCore => _userState;

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        {
            return Task.CompletedTask;
        }

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        {
            throw new NotImplementedException("ContextPropagationToken cannot be instantiated directly in tests.");
        }
    }

    private class TestStreamReader<T>(IEnumerable<T> values) : IAsyncStreamReader<T>
    {
        private readonly IEnumerator<T> _enumerator = values.GetEnumerator();

        public T Current => _enumerator.Current;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            return Task.FromResult(_enumerator.MoveNext());
        }
    }

    private class TestStreamWriter<T> : IServerStreamWriter<T>
    {
        public List<T> Written { get; } = [];
        public Grpc.Core.WriteOptions? WriteOptions { get; set; }

        public Task WriteAsync(T message)
        {
            Written.Add(message);
            return Task.CompletedTask;
        }
    }

    private static GrpcServerInterceptor CreateLicensedInterceptor(
        MAuthenticateInfoContext? context = null,
        MTokenInfo? tokenInfo = null,
        GrpcServicesConfig? config = null,
        LicenseState? state = null,
        MultiTenantConfigs? multiTenantConfig = null,
        ILicenseGuard? licenseGuard = null)
    {
        return new GrpcServerInterceptor(
            context ?? new MAuthenticateInfoContext(false),
            tokenInfo ?? new MTokenInfo(),
            NullLogger<GrpcServerInterceptor>.Instance,
            state ?? GrpcLicensedState,
            Options.Create(config ?? new GrpcServicesConfig()),
            null,
            Options.Create(multiTenantConfig ?? new MultiTenantConfigs()),
            licenseGuard);
    }

    private sealed class DenyGrpcGuard : ILicenseGuard
    {
        private static readonly LicenseState State = LicenseState.CreateFree();
        public LicenseState Current => State;
        public LicenseTier Tier => State.Tier;
        public bool IsFreeMode => true;

        public void EnsureValid(string actionType, string? actionName = null, string? payloadHash = null,
            string? correlationId = null)
        {
        }

        public bool HasFeature(string featureName)
        {
            return !string.Equals(featureName, FreeTierFeatures.Premium.Grpc, StringComparison.OrdinalIgnoreCase);
        }

        public void EnsureFeature(string featureName)
        {
            if (!HasFeature(featureName))
                throw new InvalidOperationException("grpc feature not licensed");
        }

        public void RecordAction(LicenseActionContext context)
        {
        }

        public string GetChainToken() => "test";

        public string DecryptSecurely(string purpose, string encryptedData, Func<string, string, string> decryptor)
        {
            return decryptor("k", encryptedData);
        }
    }

    [Fact]
    public void Constructor_Allows_Null_Logger()
    {
        GrpcServerInterceptor interceptor =
            new(new MAuthenticateInfoContext(false), new MTokenInfo(), null!, GrpcLicensedState);
        Assert.NotNull(interceptor);
    }

    [Fact]
    public void Constructor_Null_Context_Throws()
    {
        Assert.Throws<MArgumentException>(() =>
            new GrpcServerInterceptor(null!, new MTokenInfo(), NullLogger<GrpcServerInterceptor>.Instance,
                GrpcLicensedState));
    }

    [Fact]
    public async Task UnaryServerHandler_Processes_Call()
    {
        MAuthenticateInfoContext ctx = new(false);
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(ctx);
        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "t" } };
        TestServerCallContext callCtx = new(headers);
        string result = await interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res"));
        Assert.Equal("res", result);
        Assert.Equal("c", ctx.CorrelationId);
        Assert.Equal("c", callCtx.ResponseTrailers.GetValue(CustomHeader.CorrelationId));
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task UnaryServerHandler_Exception_Resets_Tenant()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "t" } };
        TestServerCallContext callCtx = new(headers);
        await Assert.ThrowsAsync<MInternalException>(() =>
            interceptor.UnaryServerHandler<string, string>("req", callCtx,
                (_, _) => throw new InvalidOperationException()));
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task UnaryServerHandler_Null_Context_Throws()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            interceptor.UnaryServerHandler("req", null!, (_, _) => Task.FromResult("res")));
    }

    [Fact]
    public async Task UnaryServerHandler_MultiTenantEnabled_WithoutTenantHeader_Throws_Unauthenticated()
    {
        MTokenInfo info = new()
        {
            MultiTenantEnabled = true
        };
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(
            tokenInfo: info,
            state: GrpcAndMultiTenantLicensedState);
        Metadata headers = new() { { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext callCtx = new(headers);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res")));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task UnaryServerHandler_MultiTenantEnabled_WithMismatchedClaim_Throws_PermissionDenied()
    {
        MTokenInfo info = new()
        {
            MultiTenantEnabled = true
        };
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(
            tokenInfo: info,
            state: GrpcAndMultiTenantLicensedState);
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimConstants.TenantId, "tenant-a")
            ], "test"))
        };

        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "tenant-b" } };
        TestServerCallContext callCtx = new(headers, httpContext);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res")));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task UnaryServerHandler_MultiTenantEnabled_AuthenticatedWithoutTenantClaim_Throws_Unauthenticated()
    {
        MTokenInfo info = new()
        {
            MultiTenantEnabled = true
        };
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(
            tokenInfo: info,
            state: GrpcAndMultiTenantLicensedState);
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };

        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "tenant-a" } };
        TestServerCallContext callCtx = new(headers, httpContext);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res")));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
    }

    [Fact]
    public async Task UnaryServerHandler_MultiTenantEnabled_RequireTenantClaimDisabled_Allows_AuthenticatedWithoutClaim()
    {
        MTokenInfo info = new()
        {
            MultiTenantEnabled = true
        };
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(
            tokenInfo: info,
            state: GrpcAndMultiTenantLicensedState,
            multiTenantConfig: new MultiTenantConfigs
            {
                Enabled = true,
                RequireTenantClaimForAuthenticatedUser = false
            });
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([], "test"))
        };

        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "tenant-a" } };
        TestServerCallContext callCtx = new(headers, httpContext);

        string res = await interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res"));

        Assert.Equal("res", res);
    }

    [Fact]
    public async Task UnaryServerHandler_MultiTenantEnabled_WithoutMultiTenantLicense_Throws_PermissionDenied()
    {
        LicenseState grpcOnlyState = new()
        {
            IsValid = true,
            Tier = LicenseTier.Licensed,
            Features = [FreeTierFeatures.Premium.Grpc]
        };
        MTokenInfo info = new()
        {
            MultiTenantEnabled = true
        };
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(
            tokenInfo: info,
            state: grpcOnlyState);
        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "tenant-a" } };
        TestServerCallContext callCtx = new(headers);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res")));

        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
        Assert.Contains("multi-tenant", ex.Status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClientStreamingServerHandler_Processes_Call()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.TenantId, "t" }, { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext ctx = new(headers);
        TestStreamReader<string> reader = new(["a"]);
        string res = await interceptor.ClientStreamingServerHandler(reader, ctx, (_, _) => Task.FromResult("r"));
        Assert.Equal("r", res);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task ClientStreamingServerHandler_Exception_Resets_Tenant()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.TenantId, "t" }, { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext ctx = new(headers);
        TestStreamReader<string> reader = new([]);
        await Assert.ThrowsAsync<MInternalException>(() =>
            interceptor.ClientStreamingServerHandler<string, string>(reader, ctx,
                (_, _) => throw new InvalidOperationException()));
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task ClientStreamingServerHandler_Null_Context_Throws()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            interceptor.ClientStreamingServerHandler(new TestStreamReader<string>([]), null!,
                (_, _) => Task.FromResult("r")));
    }

    [Fact]
    public async Task ServerStreamingServerHandler_Processes_Call()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.TenantId, "t" }, { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext ctx = new(headers);
        TestStreamWriter<string> writer = new();
        await interceptor.ServerStreamingServerHandler("req", writer, ctx, (_, w, _) => w.WriteAsync("resp"));
        Assert.Single(writer.Written);
        Assert.Equal("resp", writer.Written[0]);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task ServerStreamingServerHandler_Exception_Resets_Tenant()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.TenantId, "t" }, { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext ctx = new(headers);
        await Assert.ThrowsAsync<MInternalException>(() => interceptor.ServerStreamingServerHandler("req",
            new TestStreamWriter<string>(), ctx, (_, _, _) => throw new InvalidOperationException()));
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task ServerStreamingServerHandler_Null_Context_Throws()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            interceptor.ServerStreamingServerHandler("req", new TestStreamWriter<string>(), null!,
                (_, _, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_Processes_Call()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.TenantId, "t" }, { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext ctx = new(headers);
        TestStreamReader<string> reader = new([]);
        TestStreamWriter<string> writer = new();
        await interceptor.DuplexStreamingServerHandler(reader, writer, ctx, (_, _, _) => Task.CompletedTask);
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_Exception_Resets_Tenant()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new() { { CustomHeader.TenantId, "t" }, { CustomHeader.CorrelationId, "c" } };
        TestServerCallContext ctx = new(headers);
        await Assert.ThrowsAsync<MInternalException>(() =>
            interceptor.DuplexStreamingServerHandler(new TestStreamReader<string>([]), new TestStreamWriter<string>(),
                ctx, (_, _, _) => throw new InvalidOperationException()));
        Assert.Null(TenantContext.CurrentTenantId);
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_Null_Context_Throws()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            interceptor.DuplexStreamingServerHandler(new TestStreamReader<string>([]), new TestStreamWriter<string>(),
                null!, (_, _, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task UnaryServerHandler_FreeMode_Throws_License_Error()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(state: LicenseState.CreateFree());
        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "t" } };
        TestServerCallContext callCtx = new(headers);
        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res")));
        Assert.Equal(StatusCode.PermissionDenied, ex.StatusCode);
    }

    [Fact]
    public async Task UnaryServerHandler_LicenseGuardDenies_Throws_License_Error()
    {
        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(
            state: GrpcAndMultiTenantLicensedState,
            licenseGuard: new DenyGrpcGuard());
        Metadata headers = new() { { CustomHeader.CorrelationId, "c" }, { CustomHeader.TenantId, "t" } };
        TestServerCallContext callCtx = new(headers);

        InvalidOperationException ex = await Assert.ThrowsAsync<MInternalException>(() =>
            interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res")));

        Assert.Contains("grpc feature not licensed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnaryServerHandler_RateLimit_Exceeded_Throws_ResourceExhausted()
    {
        GrpcServicesConfig config = new()
        {
            Server = new GrpcServerConfig
            {
                RateLimit = new GrpcRateLimitConfig
                {
                    Enabled = true,
                    RequestsPerMinutePerApiKey = 1
                }
            }
        };

        GrpcServerInterceptor interceptor = CreateLicensedInterceptor(config: config);
        Metadata headers = new()
        {
            { CustomHeader.CorrelationId, "c" },
            { CustomHeader.TenantId, "t" },
            { CustomHeader.ApiKey, "k1" }
        };
        TestServerCallContext callCtx1 = new(headers);
        TestServerCallContext callCtx2 = new(headers);

        string ok = await interceptor.UnaryServerHandler("req", callCtx1, (_, _) => Task.FromResult("res"));
        Assert.Equal("res", ok);

        RpcException ex = await Assert.ThrowsAsync<RpcException>(() =>
            interceptor.UnaryServerHandler("req", callCtx2, (_, _) => Task.FromResult("res")));
        Assert.Equal(StatusCode.ResourceExhausted, ex.StatusCode);
    }

    [Fact]
    public async Task UnaryServerHandler_Emits_Activity()
    {
        List<Activity> activities = [];
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == GrpcRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStarted = a => activities.Add(a);
        listener.ActivityStopped = _ => { };
        ActivitySource.AddActivityListener(listener);

        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new()
        {
            { CustomHeader.CorrelationId, "c" },
            { CustomHeader.TenantId, "t" }
        };
        TestServerCallContext callCtx = new(headers);
        _ = await interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res"));

        Assert.Contains(activities, a => a.Kind == ActivityKind.Server && a.DisplayName == "test");
    }

    [Fact]
    public async Task UnaryServerHandler_Emits_Metrics()
    {
        long requestCount = 0;
        bool sawDuration = false;

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == GrpcRuntimeTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "grpc_requests_total")
            {
                requestCount += measurement;
            }
        });
        listener.SetMeasurementEventCallback<double>((instrument, measurement, _, _) =>
        {
            if (instrument.Name == "grpc_request_duration_ms" && measurement >= 0)
            {
                sawDuration = true;
            }
        });
        listener.Start();

        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new()
        {
            { CustomHeader.CorrelationId, "c" },
            { CustomHeader.TenantId, "t" }
        };
        TestServerCallContext callCtx = new(headers);
        _ = await interceptor.UnaryServerHandler("req", callCtx, (_, _) => Task.FromResult("res"));

        Assert.True(requestCount > 0);
        Assert.True(sawDuration);
    }

    [Fact]
    public async Task AllServerCallTypes_Emit_Metrics_With_CallType_Tag()
    {
        HashSet<string> callTypes = new(StringComparer.OrdinalIgnoreCase);

        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == GrpcRuntimeTelemetry.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (!string.Equals(instrument.Name, "grpc_requests_total", StringComparison.Ordinal))
                return;

            foreach (KeyValuePair<string, object?> tag in tags)
            {
                if (string.Equals(tag.Key, "grpc.call_type", StringComparison.Ordinal) &&
                    tag.Value is string callType &&
                    !string.IsNullOrWhiteSpace(callType))
                {
                    callTypes.Add(callType);
                }
            }
        });
        listener.Start();

        GrpcServerInterceptor interceptor = CreateLicensedInterceptor();
        Metadata headers = new()
        {
            { CustomHeader.CorrelationId, "c" },
            { CustomHeader.TenantId, "t" }
        };

        _ = await interceptor.UnaryServerHandler("req", new TestServerCallContext(headers), (_, _) => Task.FromResult("res"));

        _ = await interceptor.ClientStreamingServerHandler(
            new TestStreamReader<string>(["x"]),
            new TestServerCallContext(headers),
            (_, _) => Task.FromResult("res"));

        await interceptor.ServerStreamingServerHandler(
            "req",
            new TestStreamWriter<string>(),
            new TestServerCallContext(headers),
            (_, _, _) => Task.CompletedTask);

        await interceptor.DuplexStreamingServerHandler(
            new TestStreamReader<string>([]),
            new TestStreamWriter<string>(),
            new TestServerCallContext(headers),
            (_, _, _) => Task.CompletedTask);

        Assert.Contains("unary", callTypes);
        Assert.Contains("client_streaming", callTypes);
        Assert.Contains("server_streaming", callTypes);
        Assert.Contains("duplex_streaming", callTypes);
    }

    [Fact]
    public void MetadataExtensions_GetValue_Returns_Value()
    {
        Metadata md = new() { { "a", "b" } };
        Assert.Equal("b", md.GetValue("a"));
    }

    [Fact]
    public void MetadataExtensions_GetValue_Returns_Null_When_Missing()
    {
        Metadata md = [];
        Assert.Null(md.GetValue("missing"));
    }

    [Fact]
    public void MetadataExtensions_Null_Metadata_Throws()
    {
        Assert.Throws<MArgumentException>(() => MetadataExtensions.GetValue(null!, "a"));
    }
}
