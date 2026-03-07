namespace Muonroi.Governance.Tests;

[CollectionDefinition("NonParallel", DisableParallelization = true)]
public class NonParallelCollection : ICollectionFixture<object>
{
}
