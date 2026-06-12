namespace Muonroi.BuildingBlock.Test;

public class MDapperExtensionsTests
{
    [Fact]
    public async Task QueryPageAsync_Returns_Data()
    {
        IDapper dapper = Substitute.For<IDapper>();
        PageResult<string> expected = new()
        {
            Result = ["a"],
            Page = 1,
            PageSize = 10,
            TotalCount = 1,
            TotalPage = 1
        };
        dapper.QueryPageAsync<string>("COUNT", "SQL", 1, 10, Arg.Any<object?>(), null, Arg.Any<bool?>(), null, null,
            Arg.Any<bool>(), CancellationToken.None).Returns(expected);

        MDapperCommand cmd = new()
        {
            CommandText = "SQL",
            Parameters = new { }
        };
        PageResult<string> result = await dapper.QueryPageAsync<string>(cmd, "COUNT");

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task QueryPageAsync_No_Data_Returns_Empty_List()
    {
        IDapper dapper = Substitute.For<IDapper>();
        PageResult<string> expected = new()
        {
            Result = [],
            Page = 1,
            PageSize = 10,
            TotalCount = 0,
            TotalPage = 0
        };
        dapper.QueryPageAsync<string>("COUNT", "SQL", 1, 10, Arg.Any<object?>(), null, Arg.Any<bool?>(), null, null,
            Arg.Any<bool>(), CancellationToken.None).Returns(expected);

        MDapperCommand cmd = new()
        {
            CommandText = "SQL"
        };
        PageResult<string> result = await dapper.QueryPageAsync<string>(cmd, "COUNT");

        Assert.Empty(result.Result);
    }

    [Fact]
    public async Task QueryPageAsync_Command_Null_Throws()
    {
        IDapper dapper = Substitute.For<IDapper>();
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            dapper.QueryPageAsync<string>(null!, "COUNT"));
    }

    [Fact]
    public async Task QueryPlainPageAsync_Returns_Data()
    {
        IDapper dapper = Substitute.For<IDapper>();
        List<string> expected = ["a", "b"];
        dapper.QueryPlainPageAsync<string>("SQL", 1, 10, Arg.Any<object?>(), null, Arg.Any<bool?>(), null, null,
            Arg.Any<bool>(), CancellationToken.None).Returns(expected);

        MDapperCommand cmd = new()
        {
            CommandText = "SQL"
        };
        List<string> result = await dapper.QueryPlainPageAsync<string>(cmd);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task QueryPlainPageAsync_No_Data_Returns_Empty_List()
    {
        IDapper dapper = Substitute.For<IDapper>();
        List<string> expected = [];
        dapper.QueryPlainPageAsync<string>("SQL", 1, 10, Arg.Any<object?>(), null, Arg.Any<bool?>(), null, null,
            Arg.Any<bool>(), CancellationToken.None).Returns(expected);

        MDapperCommand cmd = new()
        {
            CommandText = "SQL"
        };
        List<string> result = await dapper.QueryPlainPageAsync<string>(cmd);

        Assert.Empty(result);
    }

    [Fact]
    public async Task QueryPlainPageAsync_Command_Null_Throws()
    {
        IDapper dapper = Substitute.For<IDapper>();
        await Assert.ThrowsAsync<NullReferenceException>(() =>
            dapper.QueryPlainPageAsync<string>(null!));
    }
}
