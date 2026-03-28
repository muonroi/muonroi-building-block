using Microsoft.EntityFrameworkCore;
using MuonroiService.Core.Infrastructure;

namespace MuonroiService.Sites.Default;

/// <summary>
/// Default site EF Core DbContext — inherits shared configuration from <see cref="ContextBase"/>.
/// </summary>
public sealed class DefaultOrderContext : ContextBase
{
    public DefaultOrderContext(DbContextOptions<DefaultOrderContext> options)
        : base(options)
    {
    }
}
