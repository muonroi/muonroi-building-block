namespace Muonroi.BuildingBlock.Test.Helpers;

internal static class AsyncEnumerableHelper
{
    public static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}