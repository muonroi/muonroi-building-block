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
            // ResolveGitCommit spawns a `git` child process whose working directory is tempRoot. On
            // Windows the child can keep a transient handle on that directory for a short window after
            // it exits, so an immediate recursive delete intermittently throws
            // IOException("...because it is being used by another process") under full-suite load. Retry
            // briefly, then give up — leaving a temp dir behind must not fail an otherwise-passing test.
            DeleteDirectoryBestEffort(tempRoot);
        }
    }

    private static void DeleteDirectoryBestEffort(string path)
    {
        // Best-effort only: the assertion under test has already completed by the time cleanup runs, so
        // a failure to delete the throwaway temp directory must never fail the test. The `git` child
        // process spawned by ResolveGitCommit (working directory = path) can keep a transient handle on
        // the directory for a while after it exits, so retry for a bounded window and then give up
        // silently — the OS reclaims the temp directory regardless.
        for (int attempt = 0; attempt < 40; attempt++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }

                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(100);
            }
        }
    }
}
