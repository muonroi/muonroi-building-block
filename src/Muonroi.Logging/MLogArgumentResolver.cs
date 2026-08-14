namespace Muonroi.Logging;

/// <summary>
/// Default implementation of <see cref="IMLogArgumentResolver"/> that provides
/// safe JSON serialization, cycle reference ignoring, and basic PII masking.
/// </summary>
public sealed class MLogArgumentResolver : IMLogArgumentResolver
{
    private static readonly JsonSerializerOptions _jsonOptions;
    
    // Regex for basic masking of common sensitive patterns (JWT, Bearer, etc.)
    private static readonly Regex _jwtRegex = new(@"\b[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex _bearerRegex = new(@"\bBearer\s+[A-Za-z0-9\-_\.=]{10,}", RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex _emailRegex = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    static MLogArgumentResolver()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            MaxDepth = 8,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    /// <inheritdoc />
    public object? Resolve(object? argument)
    {
        if (argument == null)
            return null;

        // Primitives and strings are masked directly
        if (argument is string strArg)
        {
            return MaskString(strArg);
        }
        if (argument.GetType().IsPrimitive || argument is decimal || argument is DateTime || argument is Guid)
        {
            return argument;
        }

        try
        {
            // Serialize to JSON securely
            string json = JsonSerializer.Serialize(argument, _jsonOptions);
            
            // Mask common sensitive data in the JSON string
            json = MaskString(json);
            
            // Truncate if too large to prevent log bloat (e.g. > 16KB)
            if (json.Length > 16384)
            {
                json = json.Substring(0, 16384) + "... [TRUNCATED DUE TO SIZE]";
            }

            return json;
        }
        catch (Exception ex)
        {
            return $"[Serialization Error: {ex.Message}]";
        }
    }

    private static string MaskString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        input = _jwtRegex.Replace(input, "***JWT***");
        input = _bearerRegex.Replace(input, "Bearer ***MASKED***");
        input = _emailRegex.Replace(input, "***@***.***");

        // Basic property masking for passwords/tokens in JSON
        input = Regex.Replace(input, @"(?i)(""password""\s*:\s*"")[^""]+("")", "$1***$2", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
        input = Regex.Replace(input, @"(?i)(""token""\s*:\s*"")[^""]+("")", "$1***$2", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        return input;
    }
}
