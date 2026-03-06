namespace Muonroi.BuildingBlock.Test;

[CollectionDefinition("NonParallel", DisableParallelization = true)]
public class NonParallelCollection : ICollectionFixture<object>
{
}
