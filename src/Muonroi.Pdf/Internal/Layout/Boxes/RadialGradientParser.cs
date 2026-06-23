using System.Collections.Generic;
using System.Globalization;

namespace Muonroi.Pdf.Internal.Layout.Boxes;

/// <summary>
/// Parses a CSS <c>radial-gradient(...)</c> function (Phase 15). Supports the locked subset (D-04/D-05):
/// optional shape keyword (<c>circle</c>|<c>ellipse</c>, default ellipse), optional <c>at &lt;position&gt;</c>
/// keyword position, and two or more color stops. Returns <see langword="false"/> on any malformed
/// input rather than throwing.
/// </summary>
internal static class RadialGradientParser
{
    // Gradient-definition keywords (shape, size, "at") that distinguish the first comma-separated
    // part as a gradient definition vs a color stop.
    private static readonly string[] ShapeKeywords = ["circle", "ellipse"];
    private static readonly string[] ExtentKeywords = ["farthest-corner", "farthest-side", "closest-corner", "closest-side"];
    // Note: "at" is deliberately NOT a keyword here — matching it via Contains() misclassifies named
    // colors that embed the substring "at" (e.g. "wheat", "chocolate") as gradient-definition parts.
    // The actual position VALUE (top/bottom/left/right/center) is what flags a definition part; the
    // "at" connector is parsed separately in ParseShapeAndPosition via " at " / "at " matching.
    private static readonly string[] PositionKeywords = ["top", "bottom", "left", "right", "center"];

    public static bool TryParse(string css, out RadialGradient gradient)
    {
        gradient = new RadialGradient();
        if (string.IsNullOrWhiteSpace(css))
            return false;

        int open = css.IndexOf("radial-gradient(", System.StringComparison.OrdinalIgnoreCase);
        if (open < 0)
            return false;

        int argsStart = open + "radial-gradient(".Length;
        int argsEnd = MatchParen(css, argsStart - 1);
        if (argsEnd < 0)
            return false;

        string args = css.Substring(argsStart, argsEnd - argsStart);
        List<string> parts = SplitTopLevel(args, ',');
        if (parts.Count == 0)
            return false;

        // Determine whether the first part is a gradient definition (shape/size/position) or a color stop.
        string shape = "ellipse";
        float posX = 0.5f;
        float posY = 0.5f;
        int firstStopIndex = 0;

        if (IsGradientDefinitionPart(parts[0]))
        {
            ParseShapeAndPosition(parts[0], ref shape, ref posX, ref posY);
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

        gradient = new RadialGradient { Shape = shape, PositionX = posX, PositionY = posY, Stops = stops };
        return true;
    }

    // Returns true if the first comma-separated part contains a gradient definition keyword
    // (shape, extent, or position). If it's only colors (e.g. "#fff") treat as a color stop.
    private static bool IsGradientDefinitionPart(string part)
    {
        string lower = part.Trim().ToLowerInvariant();
        foreach (string kw in ShapeKeywords)
            if (lower.Contains(kw)) return true;
        foreach (string kw in ExtentKeywords)
            if (lower.Contains(kw)) return true;
        foreach (string kw in PositionKeywords)
            if (lower.Contains(kw)) return true;
        return false;
    }

    private static void ParseShapeAndPosition(string part, ref string shape, ref float posX, ref float posY)
    {
        string lower = part.Trim().ToLowerInvariant();

        // Shape detection.
        if (lower.Contains("circle")) shape = "circle";
        else if (lower.Contains("ellipse")) shape = "ellipse";
        // else keep default "ellipse"

        // Position detection: look for "at <keyword>" — either " at " mid-string or an "at " prefix.
        int atIdx = lower.IndexOf(" at ", System.StringComparison.Ordinal);

        string posStr;
        if (atIdx >= 0)
        {
            posStr = lower[(atIdx + 4)..].Trim();
        }
        else if (lower.StartsWith("at "))
        {
            posStr = lower[3..].Trim();
        }
        else
        {
            return; // no position keyword found; keep defaults
        }

        // Parse keyword position pairs (e.g. "top left", "center", "right bottom").
        bool hasTop = posStr.Contains("top");
        bool hasBottom = posStr.Contains("bottom");
        bool hasLeft = posStr.Contains("left");
        bool hasRight = posStr.Contains("right");
        bool hasCenter = posStr.Contains("center");

        if (hasLeft) posX = 0f;
        else if (hasRight) posX = 1f;
        else posX = 0.5f; // center or unspecified

        if (hasTop) posY = 0f;
        else if (hasBottom) posY = 1f;
        else posY = 0.5f; // center or unspecified
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
