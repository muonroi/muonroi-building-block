using Xunit;

namespace Muonroi.UiEngine.Catalog.Tests;

public class DummyTest
{
    [Fact]
    public void Prevent_No_Test_Available_Error()
    {
        // This test prevents the "No test is available" error during test discovery
        // when the project has not yet received any tests.
        Assert.True(true);
    }
}
