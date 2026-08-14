namespace Muonroi.AspNetCore.Tests.Extensions;

// Classes to trigger validation warnings
public class BadEntity { public int Id { get; set; } }
public class BadRepository { }
public class BadController : ControllerBase { }
public class BadHandler { }

public class ArchitectureValidationTests
{
    [Fact]
    public void EnforceArchitecture_RunsWithoutThrowing()
    {
        var services = new ServiceCollection();
        services.EnforceArchitecture(Assembly.GetExecutingAssembly());

        // Should not throw by default
        Assert.NotEmpty(services);
    }

    [Fact]
    public void EnforceArchitecture_WithViolations_ThrowsWhenRequested()
    {
        var services = new ServiceCollection();
        
        // This assembly contains BadEntity, BadRepository etc.
        Assert.Throws<MInternalException>(() => 
            services.EnforceArchitecture(Assembly.GetExecutingAssembly(), throwOnViolation: true));
    }
}
