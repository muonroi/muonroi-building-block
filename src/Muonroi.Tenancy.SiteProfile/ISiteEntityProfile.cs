namespace Muonroi.Tenancy.SiteProfile;

/// <summary>
/// Marker interface that enforces a compile-time constraint between a core entity type and its
/// site-specific subtype. The generic constraint <c>where TSite : TCore</c> guarantees that
/// <typeparamref name="TSite"/> inherits from <typeparamref name="TCore"/> — the compiler
/// rejects any usage where the site entity does not extend the core entity.
///
/// Use this interface as a type constraint on services that must work with entity hierarchies:
/// <code>
/// public class OrderService&lt;TCore, TSite&gt;
///     where TSite : TCore
///     where TSite : ISiteEntityProfile&lt;TCore, TSite&gt;
/// {
///     // TSite is guaranteed to inherit TCore at compile time.
/// }
/// </code>
///
/// The <see cref="SiteEntityMapAttribute"/> is the runtime counterpart — it carries
/// the same relationship as attribute metadata for source generators to read.
/// </summary>
/// <typeparam name="TCore">The base/core entity type shared across all sites.</typeparam>
/// <typeparam name="TSite">The site-specific entity type. Must inherit from <typeparamref name="TCore"/>.</typeparam>
public interface ISiteEntityProfile<TCore, TSite> where TSite : TCore
{
    // Marker interface — the generic constraint `where TSite : TCore` enforces
    // that the site entity inherits from the core entity at compile time.
    // Services typed against ISiteEntityProfile<CoreOrder, TciOrder> will not compile
    // if TciOrder does not inherit CoreOrder.
}
