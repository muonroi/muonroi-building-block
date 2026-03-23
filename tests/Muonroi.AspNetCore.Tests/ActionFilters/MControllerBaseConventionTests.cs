namespace Muonroi.AspNetCore.Tests.ActionFilters;

public class MControllerBaseConventionTests
{
    [Fact]
    public void Apply_EmptyApplication_DoesNotThrow()
    {
        var convention = new MControllerBaseConvention();
        var application = new ApplicationModel();

        var act = () => convention.Apply(application);

        act.Should().NotThrow();
    }
}
