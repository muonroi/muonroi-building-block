namespace Muonroi.RuleEngine.SourceGenerators.Authoring;

internal static class FactSchemaWalker
{
    private const int MaxDepth = 4;

    public static AuthoringSchemaNode? Walk(ITypeSymbol? typeSymbol, string? description = null)
    {
        if (typeSymbol is null)
        {
            return null;
        }

        HashSet<string> visited = new(StringComparer.Ordinal);
        ITypeSymbol normalized = UnwrapNullable(typeSymbol, out bool isNullable);
        return new AuthoringSchemaNode(
            normalized.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            isNullable,
            description,
            BuildFields(normalized, string.Empty, 0, visited));
    }

    private static IReadOnlyList<AuthoringSchemaField> BuildFields(
        ITypeSymbol typeSymbol,
        string parentPath,
        int depth,
        HashSet<string> visited)
    {
        if (depth >= MaxDepth)
        {
            return [];
        }

        ITypeSymbol normalized = UnwrapNullable(typeSymbol, out _);
        if (IsPrimitive(normalized))
        {
            return [];
        }

        if (TryGetCollectionElementType(normalized, out ITypeSymbol? elementType))
        {
            if (elementType is null)
            {
                return [];
            }

            string arrayPath = string.IsNullOrWhiteSpace(parentPath) ? "items" : parentPath + "[]";
            return BuildFields(elementType, arrayPath, depth + 1, visited);
        }

        string visitKey = normalized.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (!visited.Add(visitKey))
        {
            return [];
        }

        List<AuthoringSchemaField> fields = [];
        if (normalized is INamedTypeSymbol namedType)
        {
            foreach (IPropertySymbol property in namedType.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility != Accessibility.Public ||
                    property.IsStatic ||
                    property.GetMethod is null)
                {
                    continue;
                }

                ITypeSymbol propertyType = UnwrapNullable(property.Type, out bool propertyNullable);
                string pathSegment = ToCamelCase(property.Name);
                string path = string.IsNullOrWhiteSpace(parentPath) ? pathSegment : parentPath + "." + pathSegment;
                string dataType = MapDataType(propertyType);
                IReadOnlyList<AuthoringSchemaField> children = [];
                if (!IsPrimitive(propertyType))
                {
                    HashSet<string> childVisited = new(visited, StringComparer.Ordinal);
                    children = BuildFields(propertyType, path, depth + 1, childVisited);
                }

                fields.Add(new AuthoringSchemaField(
                    path,
                    ToTitleCase(property.Name),
                    dataType,
                    !propertyNullable,
                    null,
                    null,
                    children));
            }
        }

        return fields;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol typeSymbol, out bool isNullable)
    {
        isNullable = typeSymbol.NullableAnnotation == NullableAnnotation.Annotated;

        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
        {
            isNullable = true;
            return namedType.TypeArguments[0];
        }

        return typeSymbol;
    }

    private static bool TryGetCollectionElementType(ITypeSymbol typeSymbol, out ITypeSymbol? elementType)
    {
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        if (typeSymbol is INamedTypeSymbol namedType &&
            namedType.IsGenericType &&
            namedType.Name == "IEnumerable" &&
            namedType.TypeArguments.Length == 1)
        {
            elementType = namedType.TypeArguments[0];
            return true;
        }

        INamedTypeSymbol? enumerableInterface = typeSymbol.AllInterfaces
            .OfType<INamedTypeSymbol>()
            .FirstOrDefault(interfaceType => interfaceType.Name == "IEnumerable" && interfaceType.TypeArguments.Length == 1);
        if (enumerableInterface is not null)
        {
            elementType = enumerableInterface.TypeArguments[0];
            return true;
        }

        elementType = null;
        return false;
    }

    private static bool IsPrimitive(ITypeSymbol typeSymbol)
    {
        if (typeSymbol.SpecialType != SpecialType.None)
        {
            return typeSymbol.SpecialType switch
            {
                SpecialType.System_Boolean => true,
                SpecialType.System_Byte => true,
                SpecialType.System_Char => true,
                SpecialType.System_Decimal => true,
                SpecialType.System_Double => true,
                SpecialType.System_Int16 => true,
                SpecialType.System_Int32 => true,
                SpecialType.System_Int64 => true,
                SpecialType.System_SByte => true,
                SpecialType.System_Single => true,
                SpecialType.System_String => true,
                SpecialType.System_UInt16 => true,
                SpecialType.System_UInt32 => true,
                SpecialType.System_UInt64 => true,
                _ => false
            };
        }

        string fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        return string.Equals(fullName, "global::System.Guid", StringComparison.Ordinal) ||
               string.Equals(fullName, "global::System.DateTime", StringComparison.Ordinal) ||
               string.Equals(fullName, "global::System.DateOnly", StringComparison.Ordinal) ||
               string.Equals(fullName, "global::System.TimeOnly", StringComparison.Ordinal);
    }

    private static string MapDataType(ITypeSymbol typeSymbol)
    {
        if (TryGetCollectionElementType(typeSymbol, out _))
        {
            return "array";
        }

        if (typeSymbol.SpecialType != SpecialType.None)
        {
            const string numberStringType = "number";
            return typeSymbol.SpecialType switch
            {
                SpecialType.System_Boolean => "boolean",
                SpecialType.System_Byte => numberStringType,
                SpecialType.System_Char => "string",
                SpecialType.System_Decimal => numberStringType,
                SpecialType.System_Double => numberStringType,
                SpecialType.System_Int16 => numberStringType,
                SpecialType.System_Int32 => numberStringType,
                SpecialType.System_Int64 => numberStringType,
                SpecialType.System_SByte => numberStringType,
                SpecialType.System_Single => numberStringType,
                SpecialType.System_String => "string",
                SpecialType.System_UInt16 => numberStringType,
                SpecialType.System_UInt32 => numberStringType,
                SpecialType.System_UInt64 => numberStringType,
                _ => "object"
            };
        }

        string fullName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (string.Equals(fullName, "global::System.Guid", StringComparison.Ordinal))
        {
            return "string";
        }

        if (string.Equals(fullName, "global::System.DateTime", StringComparison.Ordinal) ||
            string.Equals(fullName, "global::System.DateOnly", StringComparison.Ordinal) ||
            string.Equals(fullName, "global::System.TimeOnly", StringComparison.Ordinal))
        {
            return "date";
        }

        return "object";
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length == 1 ? value.ToLowerInvariant() : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        List<char> chars = [];
        for (int index = 0; index < value.Length; index += 1)
        {
            char current = value[index];
            if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
            {
                chars.Add(' ');
            }
            chars.Add(current);
        }

        return new string([.. chars]);
    }
}
