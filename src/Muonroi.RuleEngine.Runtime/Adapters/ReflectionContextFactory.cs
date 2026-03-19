using System.Globalization;
using System.Reflection;
using Muonroi.RuleEngine.Abstractions.Adapters;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Default <see cref="IContextFactory{TContext}"/> that reconstructs a
/// <typeparamref name="TContext"/> from a <see cref="FactBag"/> using reflection.
/// Requires <typeparamref name="TContext"/> to have a parameterless constructor.
/// Matches fact keys to writable properties (case-insensitive, also tries camelCase).
/// </summary>
internal sealed class ReflectionContextFactory<TContext>
    : IContextFactory<TContext>
    where TContext : new()
{
    public TContext Create(FactBag facts)
    {
        TContext ctx = new();
        foreach (PropertyInfo prop in typeof(TContext).GetProperties(
            BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite))
        {
            // try exact name first, then camelCase
            object? value = null;
            if (!facts.TryGet<object>(prop.Name, out value))
            {
                facts.TryGet<object>(ToCamelCase(prop.Name), out value);
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
        => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);
}
