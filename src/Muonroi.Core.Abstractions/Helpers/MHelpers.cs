namespace Muonroi.Core.Abstractions.Helpers;

public static class MHelpers
{
    private static readonly ConcurrentDictionary<string, Dictionary<string, string>> ErrorMessages = new();
    private static ResourceSetting? _resourceSetting;
    private static Assembly? _resourceAssembly;

    public static void Initialize(ResourceSetting resourceSetting, Assembly resourceAssembly)
    {
        ErrorMessages.Clear();
        _resourceSetting = resourceSetting;
        _resourceAssembly = resourceAssembly;
    }

    public static string GenerateErrorResult(string propertyName, object propertyValue)
    {
        return $"{char.ToLowerInvariant(propertyName[0])}{propertyName[1..]}: {propertyValue}";
    }

    public static string GetErrorMessage(string errorCode, string? lang = null)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            lang = _resourceSetting != null &&
                   _resourceSetting.TryGetValue(ResourceSettingKeys.Lang, out string? value)
                ? value
                : null;
        }

        lang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang;
        return GetErrorMessageInternal(errorCode, lang);
    }

    private static string GetErrorMessageInternal(string? errorCode, string lang)
    {
        const string defaultMessage = "No pre-defined error message";
        if (string.IsNullOrWhiteSpace(errorCode) || _resourceAssembly == null)
        {
            return defaultMessage;
        }

        string key = $"{_resourceAssembly.GetName().Name}@@{lang}";
        if (ErrorMessages.TryGetValue(key, out Dictionary<string, string>? cached))
        {
            return cached.TryGetValue(errorCode, out string? value) ? value : defaultMessage;
        }

        Dictionary<string, string> loaded = LoadErrorMessages(_resourceAssembly, lang);
        ErrorMessages[key] = loaded;
        return loaded.TryGetValue(errorCode, out string? msg) ? msg : defaultMessage;
    }

    private static Dictionary<string, string> LoadErrorMessages(Assembly resourceAssembly, string lang)
    {
        try
        {
            string resourceName = $"{nameof(SystemSettingKey.ResourceName).GetSettingValue()}-{lang}.json";
            string payload = GetFromResources(resourceName, resourceAssembly);
            return string.IsNullOrWhiteSpace(payload)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, string>>(payload) ?? []; // MBB002-exempt: static-class boundary
        }
        catch
        {
            return [];
        }
    }

    private static string GetFromResources(string resourceName, Assembly resourceAssembly)
    {
        using Stream? stream =
            resourceAssembly.GetManifestResourceStream($"{resourceAssembly.GetName().Name}.{resourceName}");
        if (stream == null)
        {
            return string.Empty;
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    public static string GetSettingValue(this string key)
    {
        return _resourceSetting != null && _resourceSetting.TryGetValue(key, out string? value) ? value : "vi-VN";
    }
}
