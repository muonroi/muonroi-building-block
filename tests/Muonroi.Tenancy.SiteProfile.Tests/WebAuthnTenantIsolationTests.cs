namespace Muonroi.Tenancy.SiteProfile.Tests;

public class WebAuthnTenantIsolationTests
{
    [Fact]
    public void AllIgnoreQueryFiltersCallsHaveTenantIdFilter()
    {
        // Try to locate the source file by walking up from the assembly location
        var currentDir = AppContext.BaseDirectory;
        string? projectRoot = currentDir;
        
        while (projectRoot != null && !File.Exists(Path.Combine(projectRoot, "Muonroi.Tenancy.SiteProfile.Tests.csproj")))
        {
            projectRoot = Path.GetDirectoryName(projectRoot);
        }

        projectRoot.Should().NotBeNull("Could not find project root directory");
        
        // From tests/Muonroi.Tenancy.SiteProfile.Tests/ to muonroi-building-block/src/Muonroi.Data.EntityFrameworkCore/...
        var sourceFile = Path.GetFullPath(Path.Combine(projectRoot!, "..", "..", "src", "Muonroi.Data.EntityFrameworkCore", "Auth", "EfWebAuthnCredentialStore.cs"));
        
        File.Exists(sourceFile).Should().BeTrue($"Source file not found at {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        // Regex to find .IgnoreQueryFilters()
        var matches = Regex.Matches(content, @"\.IgnoreQueryFilters\(\)");
        matches.Count.Should().BeGreaterThan(0, "Expected some .IgnoreQueryFilters() calls in EfWebAuthnCredentialStore.cs");

        // For each match, verify that tenantId filter checks (x.TenantId == tenantId or x.TenantId == credential.TenantId) follows within the same statement
        foreach (Match match in matches)
        {
            // Take a chunk of code after the call to check for the filter
            var startIndex = match.Index;
            var length = Math.Min(500, content.Length - startIndex);
            var substring = content.Substring(startIndex, length);
            
            // It should contain the tenant filter before the next semicolon (end of statement)
            var statementEnd = substring.IndexOf(';');
            if (statementEnd > 0)
            {
                substring = substring.Substring(0, statementEnd);
            }

            var hasTenantFilter = substring.Contains("x.TenantId == tenantId") || substring.Contains("x.TenantId == credential.TenantId");
            hasTenantFilter.Should().BeTrue("Every .IgnoreQueryFilters() must be followed by a TenantId filter in the same statement for safety.");
        }
    }

    [Fact]
    public async Task TenantContext_IsAsyncLocalIsolated()
    {
        var tenant1 = "tenant-1-" + Guid.NewGuid();
        var tenant2 = "tenant-2-" + Guid.NewGuid();

        TenantContext.CurrentTenantId = tenant1;
        TenantContext.CurrentTenantId.Should().Be(tenant1);

        await Task.Run(async () =>
        {
            TenantContext.CurrentTenantId.Should().Be(tenant1);

            TenantContext.CurrentTenantId = tenant2;
            TenantContext.CurrentTenantId.Should().Be(tenant2);
            await Task.Yield();
            TenantContext.CurrentTenantId.Should().Be(tenant2);

            TenantContext.CurrentTenantId = tenant1;
        });

        TenantContext.CurrentTenantId.Should().Be(tenant1);
        TenantContext.CurrentTenantId = null;
    }

    [Fact]
    public void TenantContext_NullTenantId_ReturnsNull()
    {
        TenantContext.CurrentTenantId = null;
        TenantContext.CurrentTenantId.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentTenants_AreIsolated()
    {
        var tenant1 = "concurrent-1";
        var tenant2 = "concurrent-2";

        var t1 = Task.Run(async () =>
        {
            TenantContext.CurrentTenantId = tenant1;
            for (int i = 0; i < 100; i++)
            {
                TenantContext.CurrentTenantId.Should().Be(tenant1);
                await Task.Yield();
            }
        });

        var t2 = Task.Run(async () =>
        {
            TenantContext.CurrentTenantId = tenant2;
            for (int i = 0; i < 100; i++)
            {
                TenantContext.CurrentTenantId.Should().Be(tenant2);
                await Task.Yield();
            }
        });

        await Task.WhenAll(t1, t2);
    }

    [Fact]
    public void TenantContext_AllowCrossTenantAccess_IsIsolated()
    {
        TenantContext.AllowCrossTenantAccess = false;
        
        using (var scope = Task.Run(() => 
        {
            TenantContext.AllowCrossTenantAccess = true;
            TenantContext.AllowCrossTenantAccess.Should().BeTrue();
        }))
        {
            scope.Wait();
        }

        TenantContext.AllowCrossTenantAccess.Should().BeFalse();
    }
}
