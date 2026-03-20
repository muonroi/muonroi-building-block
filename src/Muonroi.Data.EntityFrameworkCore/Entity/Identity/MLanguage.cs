namespace Muonroi.Data.EntityFrameworkCore.Entity.Identity;

/// <summary>
/// Represents a supported UI language.
/// </summary>
[Table("MLanguages")]
public sealed class MLanguage : MEntity
{
    /// <summary>
    /// The maximum name length.
    /// </summary>
    public new const int MaxNameLength = 128;

    /// <summary>
    /// The maximum display name length.
    /// </summary>
    public const int MaxDisplayNameLength = 64;

    /// <summary>
    /// The maximum icon length.
    /// </summary>
    public const int MaxIconLength = 128;

    /// <summary>
    /// Gets or sets the name of the culture, like "en" or "en-US".
    /// </summary>
    [Required]
    [StringLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [Required]
    [StringLength(MaxDisplayNameLength)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the icon.
    /// </summary>
    [StringLength(MaxIconLength)]
    public string? Icon { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the language is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MLanguage"/> class.
    /// </summary>
    public MLanguage()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="MLanguage"/> class.</summary>
    /// <param name="name">The culture name.</param>
    /// <param name="displayName">The display name.</param>
    /// <param name="icon">The optional icon.</param>
    /// <param name="isDisabled">Whether the language is disabled.</param>
    public MLanguage(string name, string displayName, string? icon = null, bool isDisabled = false)
    {
        Name = name;
        DisplayName = displayName;
        Icon = icon;
        IsDisabled = isDisabled;
    }

    /// <summary>Converts this entity to a language info model.</summary>
    /// <returns>The language info model.</returns>
    public MLanguageInfo ToLanguageInfo()
    {
        return new MLanguageInfo(Name, DisplayName, Icon, isDisabled: IsDisabled);
    }
}
