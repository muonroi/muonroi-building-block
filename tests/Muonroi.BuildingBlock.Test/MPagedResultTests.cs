namespace Muonroi.BuildingBlock.Test;

public class MPagedResultTests
{
    [Fact]
    public void Constructor_Initializes_Items_To_Empty()
    {
        MPagedResult<string> result = new();
        Assert.NotNull(result.Items);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Items_Returns_Set_Items()
    {
        MPagedResult<string> result = new()
        {
            Items = ["1", "2", "3"]
        };

        Assert.Equal(["1", "2", "3"], result.Items);

        result.Items = [];
        Assert.Empty(result.Items);

        result.Items = null!;
        Assert.Null(result.Items);
    }
}
