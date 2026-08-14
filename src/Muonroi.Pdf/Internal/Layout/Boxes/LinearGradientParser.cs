namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Parses a CSS <c>linear-gradient(...)</c> function (Phase 14). Supports an optional leading angle
/// (<c>Ndeg/rad/grad/turn</c>) or <c>to &lt;side(s)&gt;</c> direction, followed by two or more color
/// stops with optional percentage positions. <c>radial-gradient</c> / <c>conic-gradient</c> and
/// repeating gradients are out of scope (rejected by policy before reaching here). Total function:
/// returns <see langword="false"/> on any malformed input rather than throwing.
/// </summary>
internal static class LinearGradientParser
{
    public static bool TryParse(string css, out LinearGradient gradient)
    {
        gradient = new LinearGradient();
        if (string.IsNullOrWhiteSpace(css))
            return false;

        int open = css.IndexOf("linear-gradient(", System.StringComparison.OrdinalIgnoreCase);
        if (open < 0)
            return false;

        int argsStart = open + "linear-gradient(".Length;
        int argsEnd = MatchParen(css, argsStart - 1);
        if (argsEnd < 0)
            return false;

        string args = css.Substring(argsStart, argsEnd - argsStart);
        List<string> parts = SplitTopLevel(args, ',');
        if (parts.Count == 0)
            return false;

        float angle = 180f;
        int firstStopIndex = 0;
        if (IsDirection(parts[0]))
        {
            angle = ParseAngle(parts[0]);
            firstStopIndex = 1;
        }

        var stops = new List<GradientStop>();
        for (int i = firstStopIndex; i < parts.Count; i++)
        {
            string token = parts[i].Trim();
            if (token.Length == 0)
                continue;
            stops.Add(ParseStop(token));
        }

        if (stops.Count < 2)
            return false;

        gradient = new LinearGradient { AngleDegrees = angle, Stops = stops };
        return true;
    }

    private static GradientStop ParseStop(string token)
    {
        int depth = 0;
        int lastSpace = -1;
        for (int i = 0; i < token.Length; i++)
        {
            char c = token[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (char.IsWhiteSpace(c) && depth == 0) lastSpace = i;
        }

        if (lastSpace > 0)
        {
            string tail = token[(lastSpace + 1)..].Trim();
            float? pos = ParsePositionFraction(tail);
            if (pos is not null)
                return new GradientStop(token[..lastSpace].Trim(), pos);
        }

        return new GradientStop(token, null);
    }

    private static float? ParsePositionFraction(string s)
    {
        if (s.EndsWith("%", System.StringComparison.Ordinal)
            && float.TryParse(s.AsSpan(0, s.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out float pct))
        {
            return System.Math.Clamp(pct / 100f, 0f, 1f);
        }
        return null;
    }

    private static bool IsDirection(string token)
    {
        string s = token.Trim().ToLowerInvariant();
        if (s.StartsWith("to ", System.StringComparison.Ordinal))
            return true;
        return EndsWithUnit(s, "deg") || EndsWithUnit(s, "turn")
            || EndsWithUnit(s, "rad") || EndsWithUnit(s, "grad");
    }

    private static bool EndsWithUnit(string s, string unit)
    {
        if (!s.EndsWith(unit, System.StringComparison.Ordinal))
            return false;
        return float.TryParse(
            s.AsSpan(0, s.Length - unit.Length), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }

    private static float ParseAngle(string token)
    {
        string s = token.Trim().ToLowerInvariant();
        if (s.StartsWith("to ", System.StringComparison.Ordinal))
            return ParseToSide(s[3..].Trim());

        if (TryUnit(s, "grad", out float g)) return Normalize(g * 0.9f);
        if (TryUnit(s, "turn", out float t)) return Normalize(t * 360f);
        if (TryUnit(s, "rad", out float r)) return Normalize(r * 180f / (float)System.Math.PI);
        if (TryUnit(s, "deg", out float d)) return Normalize(d);
        return 180f;
    }

    private static bool TryUnit(string s, string unit, out float value)
    {
        value = 0f;
        if (!s.EndsWith(unit, System.StringComparison.Ordinal))
            return false;
        return float.TryParse(
            s.AsSpan(0, s.Length - unit.Length), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static float ParseToSide(string sides)
    {
        bool top = sides.Contains("top");
        bool bottom = sides.Contains("bottom");
        bool left = sides.Contains("left");
        bool right = sides.Contains("right");

        if (top && right) return 45f;
        if (bottom && right) return 135f;
        if (bottom && left) return 225f;
        if (top && left) return 315f;
        if (top) return 0f;
        if (right) return 90f;
        if (bottom) return 180f;
        if (left) return 270f;
        return 180f;
    }

    private static float Normalize(float deg)
    {
        float d = deg % 360f;
        return d < 0 ? d + 360f : d;
    }

    /// <summary>Index of the ')' matching the '(' at <paramref name="openIndex"/>, or -1.</summary>
    private static int MatchParen(string s, int openIndex)
    {
        int depth = 0;
        for (int i = openIndex; i < s.Length; i++)
        {
            if (s[i] == '(') depth++;
            else if (s[i] == ')')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private static List<string> SplitTopLevel(string s, char sep)
    {
        var parts = new List<string>();
        int depth = 0;
        int start = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == sep && depth == 0)
            {
                parts.Add(s[start..i]);
                start = i + 1;
            }
        }
        parts.Add(s[start..]);
        return parts;
    }
}
