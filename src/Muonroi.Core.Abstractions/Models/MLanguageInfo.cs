namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Represents an available language for the application.
/// </summary>
/// <remarks>
/// Creates a new <see cref="MLanguageInfo"/> object.
/// </remarks>
/// <param name="name">
/// Code name of the language.
/// It should be valid culture code.
/// Ex: "en-US" for American English, "tr-TR" for Turkey Turkish.
/// </param>
/// <param name="displayName">
/// Display name of the language in it's original language.
/// Ex: "English" for English, "Türkçe" for Turkish.
/// </param>
/// <param name="icon">An icon can be set to display on the UI</param>
/// <param name="isDefault">Is this the default language?</param>
/// <param name="isDisabled">Is this the language disabled?</param>
public class MLanguageInfo(string name, string displayName, string? icon = null, bool isDefault = false, bool isDisabled = false)
{
    /// <summary>
    /// Code name of the language.
    /// It should be valid culture code.
    /// Ex: "en-US" for American English, "tr-TR" for Turkey Turkish.
    /// </summary>
    public string Name { get; set; } = name;

    /// <summary>
    /// Display name of the language in it's original language.
    /// Ex: "English" for English, "Türkçe" for Turkish.
    /// </summary>
    public string DisplayName { get; set; } = displayName;

    /// <summary>
    /// An icon can be set to display on the UI.
    /// </summary>
    public string? Icon { get; set; } = icon;

    /// <summary>
    /// Is this the default language?
    /// </summary>
    public bool IsDefault { get; set; } = isDefault;

    /// <summary>
    /// Is this the language disabled?
    /// </summary>
    public bool IsDisabled { get; set; } = isDisabled;

    /// <summary>
    /// Is this language Right To Left?
    /// </summary>
    public bool IsRightToLeft
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(Name).TextInfo?.IsRightToLeft ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
