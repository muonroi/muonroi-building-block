using System.Reflection;
using Muonroi.RuleEngine.Abstractions.Adapters;

namespace Muonroi.RuleEngine.Runtime.Adapters;

/// <summary>
/// Default <see cref="IContextProjector{TContext}"/> that exposes all public properties of
/// <typeparamref name="TContext"/> using their property name as the variable name.
/// Depth: one level (primitive + string properties only; nested objects are included as-is).
/// </summary>
internal sealed class ReflectionContextProjector<TContext>
    : IContextProjector<TContext>
{
    public IReadOnlyDictionary<string, object?> Project(TContext context)
    {
        if (context is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        PropertyInfo[] props = typeof(TContext).GetProperties(
            BindingFlags.Public | BindingFlags.Instance);

        Dictionary<string, object?> result = new(props.Length, StringComparer.OrdinalIgnoreCase);
        foreach (PropertyInfo prop in props)
        {
            try
            {
                result[prop.Name] = prop.GetValue(context);
            }
            catch
            {
                // skip inaccessible or exception-throwing properties
            }
        }

        return result;
    }
}
