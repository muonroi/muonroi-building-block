namespace Muonroi.Core.Extensions;

public static class MGenericTypeExtensions
{
    public static string GetGenericTypeName(this Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        string text = string.Join(",", type.GetGenericArguments().Select(t => t.Name));
        return type.Name[..type.Name.IndexOf('`')] + "<" + text + ">";
    }

    public static string GetGenericTypeName(this object value)
    {
        return value.GetType().GetGenericTypeName();
    }
}
