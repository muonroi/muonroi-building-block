namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Default <see cref="IContextFactory{TContext}"/> that reconstructs a
/// <typeparamref name="TContext"/> from a <see cref="FactBag"/> using reflection.
/// Requires <typeparamref name="TContext"/> to have a parameterless constructor.
/// Matches fact keys to writable properties (case-insensitive, also tries camelCase).
/// </summary>
public sealed class ReflectionContextFactory<TContext>
    : IContextFactory<TContext>
    where TContext : new()
{
    /// <summary>
    /// Creates a <typeparamref name="TContext"/> instance by matching fact keys to writable properties.
    /// </summary>
    /// <param name="facts"></param>
    /// <returns></returns>
    public TContext Create(FactBag facts)
    {
        TContext ctx = new();
        foreach (PropertyInfo prop in typeof(TContext).GetProperties(
            BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite))
        {
            // try exact name first, then camelCase
            if (!facts.TryGet(prop.Name, out object? value))
            {
                facts.TryGet(ToCamelCase(prop.Name), out value);
            }

            if (value is null)
            {
                continue;
            }

            try
            {
                object converted = Convert.ChangeType(value, prop.PropertyType, CultureInfo.InvariantCulture);
                prop.SetValue(ctx, converted);
            }
            catch
            {
                // skip type-incompatible values
            }
        }

        return ctx;
    }

    private static string ToCamelCase(string s)
    {
        return string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
    }
}
