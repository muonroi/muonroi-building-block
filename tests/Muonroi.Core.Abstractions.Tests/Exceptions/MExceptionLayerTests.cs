using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Core.Abstractions.Tests.Exceptions;

public class MExceptionLayerTests
{
    [Theory]
    // Presentation layer
    [InlineData("Muonroi.AspNetCore", MExceptionLayer.Presentation)]
    [InlineData("Muonroi.AspNetCore.Middleware", MExceptionLayer.Presentation)]
    [InlineData("Muonroi.Grpc.Services", MExceptionLayer.Presentation)]
    [InlineData("Muonroi.Bff.Gateway", MExceptionLayer.Presentation)]
    // D-04: Web suffix checked BEFORE generic RuleEngine
    [InlineData("Muonroi.RuleEngine.Runtime.Web.Controllers", MExceptionLayer.Presentation)]
    [InlineData("Muonroi.RuleEngine.DecisionTable.Web.Api", MExceptionLayer.Presentation)]
    // Application layer
    [InlineData("Muonroi.Mediator.Behaviors", MExceptionLayer.Application)]
    [InlineData("Muonroi.BackgroundJobs.Workers", MExceptionLayer.Application)]
    // Domain layer
    [InlineData("Muonroi.RuleEngine.Runtime", MExceptionLayer.Domain)]
    [InlineData("Muonroi.Rules.Core", MExceptionLayer.Domain)]
    [InlineData("Muonroi.Tenancy.Abstractions", MExceptionLayer.Domain)]
    [InlineData("Muonroi.Auth.Providers", MExceptionLayer.Domain)]
    [InlineData("Muonroi.AuthZ.Policies", MExceptionLayer.Domain)]
    [InlineData("Muonroi.Governance.License", MExceptionLayer.Domain)]
    [InlineData("Muonroi.Core.Abstractions", MExceptionLayer.Domain)]
    // Infrastructure layer
    [InlineData("Muonroi.Data.Postgres", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Caching.Redis", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Messaging.RabbitMq", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Logging.Serilog", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Resilience.Polly", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Integration.Http", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Http.Client", MExceptionLayer.Infrastructure)]
    [InlineData("Muonroi.Diagnostics.Health", MExceptionLayer.Infrastructure)]
    // Unknown
    [InlineData(null, MExceptionLayer.Unknown)]
    [InlineData("SomeOtherPackage", MExceptionLayer.Unknown)]
    [InlineData("Muonroi.SomethingNew", MExceptionLayer.Unknown)]
    public void DeriveLayer_Returns_Correct_Layer(string? sourcePackage, MExceptionLayer expected)
    {
        var result = MException.DeriveLayer(sourcePackage);
        result.Should().Be(expected);
    }

    [Fact]
    public void Enum_Has_Five_Values()
    {
        Enum.GetValues<MExceptionLayer>().Should().HaveCount(5);
    }

    [Fact]
    public void Enum_Values_Are_Correct()
    {
        ((int)MExceptionLayer.Unknown).Should().Be(0);
        ((int)MExceptionLayer.Presentation).Should().Be(1);
        ((int)MExceptionLayer.Application).Should().Be(2);
        ((int)MExceptionLayer.Domain).Should().Be(3);
        ((int)MExceptionLayer.Infrastructure).Should().Be(4);
    }

    [Fact]
    public void Layer_Property_Returns_DeriveLayer_Of_SourcePackage()
    {
        // Create an exception with a known SourcePackage via MInternalException
        // which sets SourcePackage from CallerFile
        var ex = new MInternalException("test");
        // The Layer property should return DeriveLayer(SourcePackage)
        ex.Layer.Should().Be(MException.DeriveLayer(ex.SourcePackage));
    }
}
