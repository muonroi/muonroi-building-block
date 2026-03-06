namespace Muonroi.Core.Abstractions.Tests;

public class MPagedResultTests
{
    [Fact]
    public void Constructor_Initializes_Items_To_Empty()
    {
        MPagedResult<string> result = new();

        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public void Items_Can_Be_Replaced()
    {
        MPagedResult<string> result = new()
        {
            Items = ["1", "2", "3"]
        };

        result.Items.Should().Equal("1", "2", "3");

        result.Items = [];
        result.Items.Should().BeEmpty();
    }
}
