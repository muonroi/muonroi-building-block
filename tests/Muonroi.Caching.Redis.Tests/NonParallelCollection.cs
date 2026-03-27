namespace Muonroi.Caching.Redis.Tests;

[CollectionDefinition("NonParallel", DisableParallelization = true)]
public class NonParallelCollection : ICollectionFixture<object>
{
}
