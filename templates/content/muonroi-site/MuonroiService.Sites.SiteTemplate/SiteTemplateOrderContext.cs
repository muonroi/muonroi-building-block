using Microsoft.EntityFrameworkCore;

namespace MuonroiService.Sites.SiteTemplate;

/// <summary>
/// SiteTemplate site EF Core DbContext — inherits shared configuration.
/// Override OnModelCreating for SiteTemplate-specific schema differences.
/// </summary>
public sealed class SiteTemplateOrderContext : ContextBase
{
    public SiteTemplateOrderContext(DbContextOptions<SiteTemplateOrderContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // SiteTemplate-specific schema overrides go here
    }
}
