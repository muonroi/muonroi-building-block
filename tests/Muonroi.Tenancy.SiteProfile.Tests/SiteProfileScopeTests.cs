using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Muonroi.Tenancy.SiteProfile.Tests;

public class SiteProfileScopeTests
{
    [Fact]
    public void ForSite_SetsCurrentToGivenProfile()
    {
        var profile = Substitute.For<ISiteProfile>();
        profile.SiteId.Returns("TCI");

        using (SiteProfileScope.ForSite(profile))
        {
            SiteProfileScope.Current.Should().Be(profile);
            SiteProfileScope.Current?.SiteId.Should().Be("TCI");
        }
    }

    [Fact]
    public void Dispose_RestoresPreviousProfile()
    {
        var profile1 = Substitute.For<ISiteProfile>();
        profile1.SiteId.Returns("TCI");

        var profile2 = Substitute.For<ISiteProfile>();
        profile2.SiteId.Returns("HNI");

        using (SiteProfileScope.ForSite(profile1))
        {
            SiteProfileScope.Current.Should().Be(profile1);

            using (SiteProfileScope.ForSite(profile2))
            {
                SiteProfileScope.Current.Should().Be(profile2);
            }

            SiteProfileScope.Current.Should().Be(profile1);
        }

        SiteProfileScope.Current.Should().BeNull();
    }

    [Fact]
    public void NestedScopes_RestoreCorrectly()
    {
        var p1 = Substitute.For<ISiteProfile>();
        var p2 = Substitute.For<ISiteProfile>();
        var p3 = Substitute.For<ISiteProfile>();

        using (SiteProfileScope.ForSite(p1))
        {
            using (SiteProfileScope.ForSite(p2))
            {
                using (SiteProfileScope.ForSite(p3))
                {
                    SiteProfileScope.Current.Should().Be(p3);
                }
                SiteProfileScope.Current.Should().Be(p2);
            }
            SiteProfileScope.Current.Should().Be(p1);
        }
    }

    [Fact]
    public void ForSite_NullProfile_ThrowsArgumentNullException()
    {
        var act = () => SiteProfileScope.ForSite(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Dispose_Idempotent_DoesNotCorrupt()
    {
        var p1 = Substitute.For<ISiteProfile>();
        var p2 = Substitute.For<ISiteProfile>();

        using (SiteProfileScope.ForSite(p1))
        {
            var scope = SiteProfileScope.ForSite(p2);
            SiteProfileScope.Current.Should().Be(p2);

            scope.Dispose();
            SiteProfileScope.Current.Should().Be(p1);

            scope.Dispose(); // Second dispose
            SiteProfileScope.Current.Should().Be(p1);
        }
    }

    [Fact]
    public async Task AsyncLocal_TaskRunIsolation()
    {
        var p1 = Substitute.For<ISiteProfile>();
        p1.SiteId.Returns("PARENT");

        using (SiteProfileScope.ForSite(p1))
        {
            SiteProfileScope.Current.Should().Be(p1);

            await Task.Run(() =>
            {
                // Child task inherits AsyncLocal
                SiteProfileScope.Current.Should().Be(p1);

                var p2 = Substitute.For<ISiteProfile>();
                p2.SiteId.Returns("CHILD");

                using (SiteProfileScope.ForSite(p2))
                {
                    SiteProfileScope.Current.Should().Be(p2);
                }

                // Restored inside child task
                SiteProfileScope.Current.Should().Be(p1);
            });

            // Parent task unaffected by child task's further changes (though here it was restored anyway)
            SiteProfileScope.Current.Should().Be(p1);
        }
    }

    [Fact]
    public async Task ConcurrentScopes_Isolated()
    {
        var p1 = Substitute.For<ISiteProfile>();
        p1.SiteId.Returns("TASK-1");

        var p2 = Substitute.For<ISiteProfile>();
        p2.SiteId.Returns("TASK-2");

        var task1 = Task.Run(async () =>
        {
            using (SiteProfileScope.ForSite(p1))
            {
                await Task.Delay(50);
                SiteProfileScope.Current.Should().Be(p1);
                return SiteProfileScope.Current.SiteId;
            }
        });

        var task2 = Task.Run(async () =>
        {
            using (SiteProfileScope.ForSite(p2))
            {
                await Task.Delay(50);
                SiteProfileScope.Current.Should().Be(p2);
                return SiteProfileScope.Current.SiteId;
            }
        });

        var results = await Task.WhenAll(task1, task2);
        results.Should().Contain(["TASK-1", "TASK-2"]);
    }
}
