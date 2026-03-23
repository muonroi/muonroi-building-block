using Muonroi.RuleGen.Services;
using FluentAssertions;
using Xunit;

namespace Muonroi.RuleGen.Tests;

public sealed class AuditMetadataServiceTests
{
    [Fact]
    public void ResolveAuthor_Returns_EnvironmentVariable_When_Set()
    {
        string? previous = Environment.GetEnvironmentVariable("GIT_AUTHOR_EMAIL");

        try
        {
            Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", "tests@muonroi.local");

            string author = AuditMetadataService.ResolveAuthor();

            author.Should().Be("tests@muonroi.local");
        }
        finally
        {
            Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", previous);
        }
    }

    [Fact]
    public void ResolveGitCommit_Returns_Null_When_WorkingDirectory_Is_Not_A_Git_Repository()
    {
        string tempRoot = Path.Combine(Path.GetTempPath(), "muonroi-rulegen-audit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            string? commit = AuditMetadataService.ResolveGitCommit(tempRoot);

            commit.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
