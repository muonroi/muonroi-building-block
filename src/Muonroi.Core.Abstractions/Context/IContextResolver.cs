namespace Muonroi.Core.Abstractions.Context;

public interface IContextResolver
{
    string? ResolveTenantId();
    string? ResolveUserId();
    string? ResolveUsername();
}

public sealed class NullContextResolver : IContextResolver
{
    public string? ResolveTenantId() => null;
    public string? ResolveUserId() => null;
    public string? ResolveUsername() => null;
}
