// Liberation Fonts © Red Hat, Inc. — SIL Open Font License 1.1 (OFL-1.1).
// https://github.com/liberationfonts/liberation-fonts
// Bundled as embedded resources; no linking required by OFL.

using System.Reflection;
using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Internal.Font;

/// <summary>
/// Static registry of Liberation Font embedded bytes and CSS family-name mapping.
/// Provides fallback fonts for templates that reference standard CSS families
/// (Times New Roman, Arial, Courier New, etc.) without @font-face declarations.
/// </summary>
internal static class BundledFonts
{
    // Canonical Liberation family names.
    internal const string FamilySans = "Liberation Sans";
    internal const string FamilySerif = "Liberation Serif";
    internal const string FamilyMono = "Liberation Mono";

    // CSS family names that map to each Liberation family (case-insensitive).
    private static readonly string[] SansAliases =
        ["Arial", "Helvetica", "Verdana", "sans-serif", FamilySans];

    private static readonly string[] SerifAliases =
        ["Times New Roman", "Times", "Georgia", "serif", FamilySerif];

    private static readonly string[] MonoAliases =
        ["Courier New", "Courier", "monospace", FamilyMono];

    // Lazy-loaded font bytes per variant.
    private static readonly Lazy<ReadOnlyMemory<byte>> _sansRegular = new(() => Load("LiberationSans-Regular.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _sansBold = new(() => Load("LiberationSans-Bold.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _sansItalic = new(() => Load("LiberationSans-Italic.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _sansBoldItalic = new(() => Load("LiberationSans-BoldItalic.ttf"));

    private static readonly Lazy<ReadOnlyMemory<byte>> _serifRegular = new(() => Load("LiberationSerif-Regular.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _serifBold = new(() => Load("LiberationSerif-Bold.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _serifItalic = new(() => Load("LiberationSerif-Italic.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _serifBoldItalic = new(() => Load("LiberationSerif-BoldItalic.ttf"));

    private static readonly Lazy<ReadOnlyMemory<byte>> _monoRegular = new(() => Load("LiberationMono-Regular.ttf"));
    private static readonly Lazy<ReadOnlyMemory<byte>> _monoBold = new(() => Load("LiberationMono-Bold.ttf"));

    private static ReadOnlyMemory<byte> Load(string filename)
    {
        string resourceName = $"Muonroi.Pdf.Internal.Font.Resources.{filename}";
        Assembly asm = typeof(BundledFonts).Assembly;
        using Stream? stream = asm.GetManifestResourceStream(resourceName);
        if (stream is null)
            throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found. Ensure the TTF file is marked as EmbeddedResource.");
        using var ms = new MemoryStream((int)stream.Length);
        stream.CopyTo(ms);
        return new ReadOnlyMemory<byte>(ms.ToArray());
    }

    /// <summary>
    /// Returns true and populated <paramref name="bytes"/> / <paramref name="canonicalFamily"/>
    /// if the CSS family name maps to a bundled Liberation font. Returns false for unrecognized
    /// families (caller keeps its own default).
    /// </summary>
    internal static bool TryGetFallback(
        string cssFamilyName,
        FontWeight weight,
        FontStyle style,
        out ReadOnlyMemory<byte> bytes,
        out string canonicalFamily)
    {
        string[] aliases;
        Lazy<ReadOnlyMemory<byte>>[] variants;
        string canon;

        if (MatchesAny(cssFamilyName, SerifAliases))
        {
            canon = FamilySerif;
            aliases = SerifAliases;
            variants = [_serifRegular, _serifBold, _serifItalic, _serifBoldItalic];
        }
        else if (MatchesAny(cssFamilyName, SansAliases))
        {
            canon = FamilySans;
            aliases = SansAliases;
            variants = [_sansRegular, _sansBold, _sansItalic, _sansBoldItalic];
        }
        else if (MatchesAny(cssFamilyName, MonoAliases))
        {
            canon = FamilyMono;
            aliases = MonoAliases;
            variants = [_monoRegular, _monoBold, _monoRegular, _monoBold]; // no italic for Mono
        }
        else
        {
            bytes = default;
            canonicalFamily = string.Empty;
            return false;
        }

        _ = aliases; // suppress unused warning; kept for documentation
        canonicalFamily = canon;

        // Select variant by weight + style. Variants order: [Regular, Bold, Italic, BoldItalic].
        bool isBold = weight >= FontWeight.Bold;
        bool isItalic = style == FontStyle.Italic || style == FontStyle.Oblique;

        bytes = (isBold, isItalic) switch
        {
            (false, false) => variants[0].Value,
            (true, false) => variants[1].Value,
            (false, true) => variants[2].Value,
            (true, true) => variants[3].Value,
        };

        return true;
    }

    /// <summary>
    /// Returns all Liberation font variants (Family, Weight, Style, Bytes) for pre-registration
    /// in FontPipeline. Only variants that have been loaded are included (lazy — loads all on call).
    /// </summary>
    internal static IEnumerable<(string Family, FontWeight Weight, FontStyle Style, ReadOnlyMemory<byte> Bytes)> AllVariants()
    {
        // Liberation Sans
        yield return (FamilySans, FontWeight.Normal, FontStyle.Normal, _sansRegular.Value);
        yield return (FamilySans, FontWeight.Bold, FontStyle.Normal, _sansBold.Value);
        yield return (FamilySans, FontWeight.Normal, FontStyle.Italic, _sansItalic.Value);
        yield return (FamilySans, FontWeight.Bold, FontStyle.Italic, _sansBoldItalic.Value);

        // Liberation Serif
        yield return (FamilySerif, FontWeight.Normal, FontStyle.Normal, _serifRegular.Value);
        yield return (FamilySerif, FontWeight.Bold, FontStyle.Normal, _serifBold.Value);
        yield return (FamilySerif, FontWeight.Normal, FontStyle.Italic, _serifItalic.Value);
        yield return (FamilySerif, FontWeight.Bold, FontStyle.Italic, _serifBoldItalic.Value);

        // Liberation Mono
        yield return (FamilyMono, FontWeight.Normal, FontStyle.Normal, _monoRegular.Value);
        yield return (FamilyMono, FontWeight.Bold, FontStyle.Normal, _monoBold.Value);
    }

    private static bool MatchesAny(string name, string[] aliases) =>
        aliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
}
