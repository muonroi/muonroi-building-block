using System.Globalization;

namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// The size kind of a single CSS Grid track (a column or row size in a track list).
/// </summary>
internal enum GridTrackKind
{
    /// <summary>A fixed length (<c>px</c>/<c>pt</c>/<c>mm</c>/…) resolved to points at parse time. See <see cref="GridTrack.Length"/>.</summary>
    Length,

    /// <summary>A percentage (<c>%</c>) of the container's axis size; stored as a 0..1 fraction and resolved at layout time (Plan 03). See <see cref="GridTrack.Percent"/>.</summary>
    Percent,

    /// <summary>A flexible <c>fr</c> track; the <c>fr</c> count is stored in <see cref="GridTrack.Fraction"/>. Distributed at layout time (Plan 03).</summary>
    Fraction,

    /// <summary>An <c>auto</c> track — content-sized at layout time (Plan 03). Also the fallback for any unparseable token.</summary>
    Auto,

    /// <summary>A <c>minmax(min, max)</c> track; <see cref="GridTrack.Min"/> / <see cref="GridTrack.Max"/> hold the two non-MinMax sub-tracks.</summary>
    MinMax,

    /// <summary>
    /// A <c>repeat(auto-fill | auto-fit, &lt;pattern&gt;)</c> placeholder. The repetition count depends on the
    /// container's inline size and is resolved at layout time (GridLayoutEngine.MaterializeTemplate),
    /// not parse time. <see cref="GridTrack.Pattern"/> holds the per-repetition track list and
    /// <see cref="GridTrack.RepeatMode"/> the fill/fit mode.
    /// </summary>
    AutoRepeat,
}

/// <summary>The repetition mode of a <c>repeat(auto-fill | auto-fit, …)</c> track.</summary>
internal enum GridAutoRepeatMode
{
    /// <summary><c>auto-fill</c>: fit as many repetitions as the container allows, keeping empty tracks.</summary>
    AutoFill,

    /// <summary><c>auto-fit</c>: like auto-fill, but empty trailing repetitions collapse (approximated here by capping repetitions at the item count).</summary>
    AutoFit,
}

/// <summary>
/// A single CSS Grid track size, parsed from one token of a
/// <c>grid-template-columns</c>/<c>grid-template-rows</c> track list (or <c>grid-auto-columns</c>/
/// <c>grid-auto-rows</c>). Produced by <see cref="ParseTrackList"/> / <see cref="ParseSingleTrack"/>
/// at box-tree build time; consumed by the GridLayoutEngine (Plan 03) to size + place tracks.
/// Parsing NEVER throws — a malformed token degrades to an <see cref="GridTrackKind.Auto"/> track.
/// </summary>
internal sealed class GridTrack
{
    /// <summary>Maximum number of expanded tracks a single <c>repeat(N, …)</c> may produce. Clamps <c>N</c> so a hostile value like <c>repeat(99999999, 1fr)</c> cannot allocate unbounded tracks (T-19-04 DoS mitigation).</summary>
    internal const int MaxRepeatCount = 1000;

    /// <summary>The size kind of this track.</summary>
    public GridTrackKind Kind { get; set; } = GridTrackKind.Auto;

    /// <summary>For <see cref="GridTrackKind.Length"/>: the fixed size in points (already resolved via ParseLength). 0 otherwise.</summary>
    public float Length { get; set; }

    /// <summary>For <see cref="GridTrackKind.Percent"/>: the percentage as a 0..1 fraction (e.g. <c>50%</c> → 0.5). Resolved against the container axis size at layout time (Plan 03). 0 otherwise.</summary>
    public float Percent { get; set; }

    /// <summary>For <see cref="GridTrackKind.Fraction"/>: the <c>fr</c> count (e.g. <c>2fr</c> → 2). 0 otherwise.</summary>
    public float Fraction { get; set; }

    /// <summary>For <see cref="GridTrackKind.MinMax"/>: the minimum sub-track (a non-MinMax track). Null otherwise.</summary>
    public GridTrack? Min { get; set; }

    /// <summary>For <see cref="GridTrackKind.MinMax"/>: the maximum sub-track (a non-MinMax track). Null otherwise.</summary>
    public GridTrack? Max { get; set; }

    /// <summary>For <see cref="GridTrackKind.AutoRepeat"/>: the per-repetition track pattern. Null otherwise.</summary>
    public List<GridTrack>? Pattern { get; set; }

    /// <summary>For <see cref="GridTrackKind.AutoRepeat"/>: fill vs fit. Ignored otherwise.</summary>
    public GridAutoRepeatMode RepeatMode { get; set; } = GridAutoRepeatMode.AutoFill;

    /// <summary>
    /// Parses a CSS track-list string (e.g. <c>"100px 1fr 2fr"</c>, <c>"repeat(3, 1fr)"</c>,
    /// <c>"minmax(50px, 1fr) auto"</c>) into an ordered list of <see cref="GridTrack"/>.
    /// Expands <c>repeat(N, &lt;track-list&gt;)</c> to N copies of the inner list (N is a positive int,
    /// CLAMPED to <see cref="MaxRepeatCount"/>). <c>repeat(auto-fill | auto-fit, &lt;pattern&gt;)</c> becomes a
    /// single <see cref="GridTrackKind.AutoRepeat"/> placeholder resolved at layout time (count depends on
    /// container size). Handles nested parens (repeat may contain minmax).
    /// Never throws — empty/null/malformed input yields an empty list.
    /// </summary>
    internal static List<GridTrack> ParseTrackList(string? value, float fontSize)
    {
        var result = new List<GridTrack>();
        if (string.IsNullOrWhiteSpace(value))
            return result;

        foreach (var token in TokenizeTopLevel(value))
        {
            if (token.StartsWith("repeat(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(')'))
            {
                ExpandRepeat(token, fontSize, result);
            }
            else
            {
                result.Add(ParseSingleTrack(token, fontSize));
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a single track token: <c>&lt;number&gt;fr</c> → <see cref="GridTrackKind.Fraction"/>;
    /// <c>auto</c> → <see cref="GridTrackKind.Auto"/>; <c>&lt;len&gt;%</c> → <see cref="GridTrackKind.Percent"/>;
    /// <c>minmax(min, max)</c> → <see cref="GridTrackKind.MinMax"/>; otherwise ParseLength → <see cref="GridTrackKind.Length"/>
    /// (falls back to <see cref="GridTrackKind.Auto"/> when unparseable). Never throws.
    /// </summary>
    internal static GridTrack ParseSingleTrack(string token, float fontSize)
    {
        string t = token.Trim();
        if (t.Length == 0)
            return new GridTrack { Kind = GridTrackKind.Auto };

        if (string.Equals(t, "auto", StringComparison.OrdinalIgnoreCase))
            return new GridTrack { Kind = GridTrackKind.Auto };

        if (t.StartsWith("minmax(", StringComparison.OrdinalIgnoreCase) && t.EndsWith(')'))
            return ParseMinMax(t, fontSize);

        // <number>fr → Fraction
        if (t.EndsWith("fr", StringComparison.OrdinalIgnoreCase))
        {
            var num = t[..^2].Trim();
            if (float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float fr) && fr >= 0f)
                return new GridTrack { Kind = GridTrackKind.Fraction, Fraction = fr };
            return new GridTrack { Kind = GridTrackKind.Auto };
        }

        // <len>% → Percent (stored as 0..1 fraction)
        if (t.EndsWith('%'))
        {
            var num = t[..^1].Trim();
            if (float.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out float pct))
                return new GridTrack { Kind = GridTrackKind.Percent, Percent = pct / 100f };
            return new GridTrack { Kind = GridTrackKind.Auto };
        }

        // Fixed length: reuse ParseLength semantics (px→pt etc.). 0 from a bare malformed token → Auto.
        float len = BoxTreeBuilder.ParseLengthPublic(t, fontSize);
        if (len > 0f)
            return new GridTrack { Kind = GridTrackKind.Length, Length = len };

        return new GridTrack { Kind = GridTrackKind.Auto };
    }

    private static GridTrack ParseMinMax(string token, float fontSize)
    {
        // token form: minmax( <min> , <max> )
        int open = token.IndexOf('(');
        string inner = token[(open + 1)..^1];
        var args = SplitTopLevel(inner, ',');
        if (args.Count != 2)
            return new GridTrack { Kind = GridTrackKind.Auto };

        var min = ParseSingleTrack(args[0], fontSize);
        var max = ParseSingleTrack(args[1], fontSize);
        return new GridTrack { Kind = GridTrackKind.MinMax, Min = min, Max = max };
    }

    private static void ExpandRepeat(string token, float fontSize, List<GridTrack> output)
    {
        // token form: repeat( <count> , <track-list> )
        int open = token.IndexOf('(');
        string inner = token[(open + 1)..^1];
        int comma = IndexOfTopLevel(inner, ',');
        if (comma < 0)
            return; // malformed → skip the repeat

        string countToken = inner[..comma].Trim();
        string innerList = inner[(comma + 1)..].Trim();

        // auto-fill / auto-fit: the repetition count depends on the container's inline size, so it
        // cannot be expanded here. Emit a single AutoRepeat placeholder carrying the pattern + mode;
        // GridLayoutEngine.MaterializeTemplate resolves the count at layout time.
        bool isAutoFill = string.Equals(countToken, "auto-fill", StringComparison.OrdinalIgnoreCase);
        bool isAutoFit = string.Equals(countToken, "auto-fit", StringComparison.OrdinalIgnoreCase);
        if (isAutoFill || isAutoFit)
        {
            var pattern = ParseTrackList(innerList, fontSize);
            if (pattern.Count == 0)
                return;
            output.Add(new GridTrack
            {
                Kind = GridTrackKind.AutoRepeat,
                RepeatMode = isAutoFit ? GridAutoRepeatMode.AutoFit : GridAutoRepeatMode.AutoFill,
                Pattern = pattern
            });
            return;
        }

        // Integer repeat(N, …): expand to N copies now.
        if (!int.TryParse(countToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count) || count <= 0)
            return;

        // DoS clamp (T-19-04): cap the expansion so a hostile count cannot allocate unbounded tracks.
        if (count > MaxRepeatCount)
            count = MaxRepeatCount;

        var expanded = ParseTrackList(innerList, fontSize);
        if (expanded.Count == 0)
            return;

        for (int i = 0; i < count; i++)
            output.AddRange(expanded);
    }

    // Splits a track-list string into top-level tokens on whitespace, treating any parenthesised
    // group (repeat(...), minmax(...)) as a single token regardless of inner whitespace/commas.
    private static List<string> TokenizeTopLevel(string value)
    {
        var tokens = new List<string>();
        int depth = 0;
        int start = -1;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }

            bool isSep = depth == 0 && char.IsWhiteSpace(c);
            if (isSep)
            {
                if (start >= 0)
                {
                    tokens.Add(value[start..i]);
                    start = -1;
                }
            }
            else if (start < 0)
            {
                start = i;
            }
        }
        if (start >= 0)
            tokens.Add(value[start..]);
        return tokens;
    }

    // Splits on a separator char that appears at paren-depth 0 only.
    private static List<string> SplitTopLevel(string value, char sep)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }
            else if (c == sep && depth == 0)
            {
                parts.Add(value[start..i]);
                start = i + 1;
            }
        }
        parts.Add(value[start..]);
        return parts;
    }

    private static int IndexOfTopLevel(string value, char sep)
    {
        int depth = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c == '(') depth++;
            else if (c == ')') { if (depth > 0) depth--; }
            else if (c == sep && depth == 0)
                return i;
        }
        return -1;
    }
}
