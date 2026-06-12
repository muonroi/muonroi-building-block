namespace Muonroi.Caching.Redis.Tests;

[Collection("NonParallel")]
public class DistributedCacheRuntimeTelemetryTests
{
    [Fact]
    public async Task MultiLevelCacheService_GetOrSet_EmitsActivity_WithTenantTag()
    {
        string? originalTenant = TenantContext.CurrentTenantId;
        TenantContext.CurrentTenantId = "tenant-cache";

        try
        {
            List<Activity> stopped = [];
            using ActivityListener listener = new();
            listener.ShouldListenTo = source => source.Name == DistributedCacheRuntimeTelemetry.ActivitySourceName;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
            listener.ActivityStopped = activity => stopped.Add(activity);
            ActivitySource.AddActivityListener(listener);

            MultiLevelCacheService service = new(
                new MemoryCache(new MemoryCacheOptions()),
                new InMemoryDistributedCache());

            string? value = await service.GetOrSetAsync("k", () => Task.FromResult<string?>("v"), 1);

            Assert.Equal("v", value);
            Activity? activity = stopped.LastOrDefault(x =>
                string.Equals(x.OperationName, "distributed-cache.get_or_set", StringComparison.Ordinal));

            Assert.NotNull(activity);
            Assert.Equal("get_or_set", activity!.GetTagItem("cache.operation"));
            Assert.Equal("tenant-cache", activity.GetTagItem("tenant.id"));
        }
        finally
        {
            TenantContext.CurrentTenantId = originalTenant;
        }
    }

    [Fact]
    public async Task RedisExtensions_SetAndGet_EmitsActivity()
    {
        List<Activity> stopped = [];
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == DistributedCacheRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => stopped.Add(activity);
        ActivitySource.AddActivityListener(listener);

        InMemoryDistributedCache cache = new();
        await cache.SetCacheAsync("activity-key", "value", 1);
        string? value = await cache.GetCacheAsync<string>("activity-key");

        Assert.Equal("value", value);
        Assert.Contains(stopped, activity =>
            string.Equals(activity.OperationName, "distributed-cache.set", StringComparison.Ordinal));
        Assert.Contains(stopped, activity =>
            string.Equals(activity.OperationName, "distributed-cache.get", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedisExtensions_RemoveAndRefresh_EmitActivity()
    {
        List<Activity> stopped = [];
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == DistributedCacheRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => stopped.Add(activity);
        ActivitySource.AddActivityListener(listener);

        InMemoryDistributedCache cache = new();
        await cache.SetCacheAsync("activity-key", "value", 1);
        await RedisExtensions.RefreshAsync(cache, "activity-key");
        await RedisExtensions.RemoveAsync(cache, "activity-key");

        Assert.Contains(stopped, activity =>
            string.Equals(activity.OperationName, "distributed-cache.refresh", StringComparison.Ordinal));
        Assert.Contains(stopped, activity =>
            string.Equals(activity.OperationName, "distributed-cache.remove", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RedisExtensions_GetOrSet_EmitsActivity()
    {
        List<Activity> stopped = [];
        using ActivityListener listener = new();
        listener.ShouldListenTo = source => source.Name == DistributedCacheRuntimeTelemetry.ActivitySourceName;
        listener.Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded;
        listener.ActivityStopped = activity => stopped.Add(activity);
        ActivitySource.AddActivityListener(listener);

        InMemoryDistributedCache cache = new();
        string? value = await cache.GetOrSetAsync("activity-key", () => Task.FromResult<string?>("value"), 1);

        Assert.Equal("value", value);
        Activity? activity = stopped.LastOrDefault(x =>
            string.Equals(x.OperationName, "distributed-cache.get_or_set", StringComparison.Ordinal));
        Assert.NotNull(activity);
        Assert.Equal("get_or_set", activity!.GetTagItem("cache.operation"));
    }
}
