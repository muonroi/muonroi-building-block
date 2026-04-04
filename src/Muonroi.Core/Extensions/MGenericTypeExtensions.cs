namespace Muonroi.Core.Extensions;

/// <summary>
/// Provides extension methods for retrieving generic type names.
/// </summary>
public static class MGenericTypeExtensions
{
    /// <summary>
    /// Gets a formatted string representing the generic type name.
    /// </summary>
    /// <param name="type">The type to get the name for.</param>
    /// <returns>A string representation of the type name, including generic arguments if applicable.</returns>
    public static string GetGenericTypeName(this Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string text = string.Join(",", type.GetGenericArguments().Select(t => t.Name));
        return type.Name[..type.Name.IndexOf('`')] + "<" + text + ">";
    }

    /// <summary>
    /// Gets a formatted string representing the generic type name of the specified object.
    /// </summary>
    /// <param name="value">The object to get the type name for.</param>
    /// <returns>A string representation of the type name.</returns>
    public static string GetGenericTypeName(this object value)
    {
        return value.GetType().GetGenericTypeName();
    }
}
