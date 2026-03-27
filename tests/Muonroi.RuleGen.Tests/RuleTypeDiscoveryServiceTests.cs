using FluentAssertions;
using Muonroi.RuleGen.Services;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class RuleTypeDiscoveryServiceTests
{
    [Fact]
    public void Discover_Finds_IRule_Implementations_And_Deduplicates_Results()
    {
        string root = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-discovery", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            File.WriteAllText(
                Path.Combine(root, "Rules.cs"),
                """
                namespace Demo.Rules;

                public interface IRule<T> {}

                public class FirstRule : IRule<OrderContext> {}
                public class DuplicateRule : Demo.Rules.IRule<OrderContext> {}
                public class DuplicateRule : Demo.Rules.IRule<OrderContext> {}
                public class IgnoredRule {}

                public class OrderContext {}
                """);

            var result = RuleTypeDiscoveryService.Discover(root);

            result.Should().Contain(x => x.ClassName == "Demo.Rules.FirstRule" && x.ContextType == "OrderContext");
            result.Should().Contain(x => x.ClassName == "Demo.Rules.DuplicateRule" && x.ContextType == "OrderContext");
            result.Count(x => x.ClassName == "Demo.Rules.DuplicateRule").Should().Be(1);
            result.Should().NotContain(x => x.ClassName.EndsWith("IgnoredRule", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void Discover_WithMissingDirectory_Throws()
    {
        Action act = () => RuleTypeDiscoveryService.Discover(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        act.Should().Throw<DirectoryNotFoundException>();
    }
}
