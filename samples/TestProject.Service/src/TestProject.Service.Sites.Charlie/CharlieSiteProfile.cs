using Microsoft.EntityFrameworkCore;
using Muonroi.Tenancy.SiteProfile;
using TestProject.Service.Core.Constants;
using TestProject.Service.Core.Infrastructure;

namespace TestProject.Service.Sites.Charlie;

/// <summary>
/// CHARLIE site EF Core DbContext — same schema as DEFAULT.
/// CHARLIE uses its own context type so the alias pattern is explicit
/// (context type passed to [GenerateSiteProfile] is always per-site).
/// </summary>
public sealed class CharlieOrderContext : ContextBase
{
    public CharlieOrderContext(DbContextOptions<CharlieOrderContext> options)
        : base(options)
    {
    }
}

/// <summary>
/// CHARLIE site profile — aliased to DEFAULT.
/// Demonstrates [SiteProfileAlias] pattern (per D-19, D-25):
/// CHARLIE reuses ALL keyed service registrations from DEFAULT without boilerplate.
/// When CHARLIE needs to diverge: remove [SiteProfileAlias], add dedicated service classes.
/// </summary>
[SiteProfileAlias(SiteIds.DEFAULT)]
[GenerateSiteProfile(SiteIds.CHARLIE, typeof(CharlieOrderContext))]
public partial class CharlieSiteProfile : ISiteProfile
{
    public string SiteId => SiteIds.CHARLIE;
}
