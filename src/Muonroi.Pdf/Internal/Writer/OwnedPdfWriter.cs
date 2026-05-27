// SPIKE - throwaway (Phase 8.5 "Owned PDF Writer" spike)
// Goal: prove an owned PDF 1.7 writer that emits content-stream operators directly from
// pre-positioned InlineBox elements — one TJ per text run, NOT one DrawString per word —
// cuts write-stage allocation enough to hit SC4 (≤288.96 MB total) AND eliminates the
// PdfSharpCore dependency.
//
// Security invariants (SEC-02):
// This writer NEVER emits /JavaScript, /Launch, /OpenAction, or /EmbeddedFile entries.
// The absence of such calls IS the enforcement.
//
// Determinism (DET-01/02/03):
// Fixed sentinel timestamp, fixed subset-prefix, fixed /ID — identical to PdfSharpCoreWriter.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Writer;

/// <summary>
/// Pure-managed PDF 1.7 writer. Zero PdfSharpCore types. Emits content-stream text operators
/// directly from the engine's already-positioned glyphs (one <c>Tj</c> per <see cref="InlineBox"/>
/// run). Fonts are embedded via the existing in-house <see cref="TrueTypeFontSubsetter"/>.
/// </summary>
internal sealed class OwnedPdfWriter : IPdfWriter
{
    // ── determinism sentinels (DET-01/02/03) ─────────────────────────────────

    // Fixed trailer /ID — same value PdfSharpCoreWriter normalizes to.
    private const string FixedTrailerId =
        "/ID [<00000000000000000000000000000000><00000000000000000000000000000000>]";

    // Fixed creation/modification date — suppresses metadata leakage (SEC-03/SEC-04).
    private const string SentinelDateString = "D:20000101000000Z";

    // ─────────────────────────────────────────────────────────────────────────

    public async ValueTask<long> WriteAsync(
        IPositionedPageList pages,
        PdfRenderOptions options,
        Stream destination,
        CancellationToken ct = default)
    {
        if (pages is not PositionedPageList pageList)
            throw new InvalidOperationException(
                "OwnedPdfWriter requires PositionedPageList from the Muonroi.Pdf engine");

        byte[] pdfBytes = BuildPdf(pageList, options, ct);
        await destination.WriteAsync(pdfBytes, 0, pdfBytes.Length, ct).ConfigureAwait(false);
        return pdfBytes.Length;
    }

    // ── top-level builder ─────────────────────────────────────────────────────

    private static byte[] BuildPdf(
        PositionedPageList pageList,
        PdfRenderOptions options,
        CancellationToken ct)
    {
        (float pageWidthPt, float pageHeightPt) = GetPageDimensions(options);

        // Build the font resource map: family → (fontObjId, subsetBytes, codepoint→GID map)
        // We need to know GID advances for the TJ operator encoding.
        var fontInfos = BuildFontInfos(pageList);

        // Collect all PDF objects in order — we'll assign sequential object IDs.
        // Objects:
        //   1  = Catalog
        //   2  = Pages (root)
        //   3..N = per-page Page dicts
        //   N+1..M = per-page Content streams
        //   M+1..P = per-font FontDescriptor, FontFile2, Font (Type1/TrueType)
        //
        // We use a PdfObjectStore that accumulates objects and emits xref at the end.

        var store = new PdfObjectStore();

        int catalogId = store.ReserveId();   // 1
        int pagesRootId = store.ReserveId(); // 2

        int pageCount = pageList.Pages.Count;
        int[] pageObjIds = new int[pageCount];
        int[] contentObjIds = new int[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            pageObjIds[i] = store.ReserveId();
            contentObjIds[i] = store.ReserveId();
        }

        // Reserve font object IDs: each embedded font needs Font dict + FontDescriptor + FontFile2
        // (3 objects per font). We only embed fonts actually present in EmbeddedFonts.
        var fontObjMap = new Dictionary<string, FontObjectIds>(StringComparer.Ordinal);
        foreach (EmbeddedFontInfo fi in pageList.EmbeddedFonts)
        {
            string key = fi.Family;
            if (!fontObjMap.ContainsKey(key))
            {
                fontObjMap[key] = new FontObjectIds(
                    FontDictId: store.ReserveId(),
                    DescriptorId: store.ReserveId(),
                    FontFileId: store.ReserveId());
            }
        }

        // ── Emit objects ────────────────────────────────────────────────────

        // Object 1: Catalog (SEC-02: no /JavaScript /OpenAction /EmbeddedFile)
        store.WriteObject(catalogId, w =>
        {
            w.WriteRawLine($"<< /Type /Catalog /Pages {pagesRootId} 0 R >>");
        });

        // Object 2: Pages root
        store.WriteObject(pagesRootId, w =>
        {
            w.WriteRaw("<< /Type /Pages /Kids [");
            for (int i = 0; i < pageCount; i++)
            {
                w.WriteRaw($" {pageObjIds[i]} 0 R");
            }
            w.WriteRawLine($" ] /Count {pageCount} >>");
        });

        // Per-page objects
        for (int i = 0; i < pageCount; i++)
        {
            ct.ThrowIfCancellationRequested();

            PositionedPage page = pageList.Pages[i];
            byte[] contentBytes = BuildContentStream(page, pageHeightPt, fontObjMap);

            // Content stream object
            store.WriteObject(contentObjIds[i], w =>
            {
                w.WriteRawLine($"<< /Length {contentBytes.Length} >>");
                w.WriteRawLine("stream");
                w.WriteBytes(contentBytes);
                w.WriteRawLine("\nendstream");
            });

            // Page dict object
            store.WriteObject(pageObjIds[i], w =>
            {
                w.WriteRaw($"<< /Type /Page /Parent {pagesRootId} 0 R");
                w.WriteRaw($" /MediaBox [0 0 {pageWidthPt.ToString("F2", CultureInfo.InvariantCulture)} {pageHeightPt.ToString("F2", CultureInfo.InvariantCulture)}]");
                w.WriteRaw($" /Contents {contentObjIds[i]} 0 R");

                // /Resources /Font dict
                if (fontObjMap.Count > 0)
                {
                    w.WriteRaw(" /Resources << /Font <<");
                    foreach (KeyValuePair<string, FontObjectIds> kv in fontObjMap)
                    {
                        // Resource name is /F0, /F1... matching what BuildContentStream emits.
                        string resName = FontResourceName(kv.Key, fontObjMap);
                        w.WriteRaw($" /{resName} {kv.Value.FontDictId} 0 R");
                    }
                    w.WriteRaw(" >> >>");
                }
                w.WriteRawLine(" >>");
            });
        }

        // Font objects
        foreach (KeyValuePair<string, FontObjectIds> kv in fontObjMap)
        {
            string family = kv.Key;
            FontObjectIds ids = kv.Value;

            // Find the EmbeddedFontInfo for this family
            EmbeddedFontInfo? fi = pageList.EmbeddedFonts.FirstOrDefault(f => f.Family == family);
            if (fi == null)
                continue;

            byte[] subsetBytes = fi.SubsetBytes.ToArray();

            // FontFile2 stream (raw TrueType subset bytes)
            store.WriteObject(ids.FontFileId, w =>
            {
                w.WriteRawLine($"<< /Length {subsetBytes.Length} /Length1 {subsetBytes.Length} >>");
                w.WriteRawLine("stream");
                w.WriteBytes(subsetBytes);
                w.WriteRawLine("\nendstream");
            });

            // FontDescriptor
            store.WriteObject(ids.DescriptorId, w =>
            {
                w.WriteRaw($"<< /Type /FontDescriptor /FontName /AAAAAA+{PdfName(family)}");
                w.WriteRaw(" /Flags 32");
                w.WriteRaw(" /FontBBox [-1000 -200 1000 900]");
                w.WriteRaw(" /ItalicAngle 0");
                w.WriteRaw(" /Ascent 900");
                w.WriteRaw(" /Descent -200");
                w.WriteRaw(" /CapHeight 700");
                w.WriteRaw(" /StemV 80");
                w.WriteRaw($" /FontFile2 {ids.FontFileId} 0 R");
                w.WriteRawLine(" >>");
            });

            // Font dict — simple TrueType (Type 1 is wrong; use /TrueType for TTF subsets)
            // We map codepoints to glyph IDs via the subset cmap and emit widths array.
            int[] widths = BuildWidthsArray(fi, out int firstChar, out int lastChar);

            store.WriteObject(ids.FontDictId, w =>
            {
                w.WriteRaw($"<< /Type /Font /Subtype /TrueType /BaseFont /AAAAAA+{PdfName(family)}");
                w.WriteRaw($" /FirstChar {firstChar} /LastChar {lastChar}");
                w.WriteRaw(" /Widths [");
                foreach (int wd in widths)
                    w.WriteRaw($" {wd}");
                w.WriteRaw(" ]");
                w.WriteRaw($" /FontDescriptor {ids.DescriptorId} 0 R");
                w.WriteRaw(" /Encoding /WinAnsiEncoding");
                w.WriteRawLine(" >>");
            });
        }

        // Info dict (deterministic/sanitized — SEC-03/SEC-04)
        int infoId = store.ReserveId();
        store.WriteObject(infoId, w =>
        {
            w.WriteRaw("<< /Producer ()");
            w.WriteRaw($" /CreationDate ({SentinelDateString})");
            w.WriteRaw($" /ModDate ({SentinelDateString})");
            w.WriteRawLine(" >>");
        });

        return store.Finalize(catalogId, infoId);
    }

    // ── content stream builder ────────────────────────────────────────────────

    /// <summary>
    /// Builds the content stream for one page. Text elements are emitted as one <c>Tj</c>
    /// per <see cref="InlineBox"/> run (NOT per word). PDF coordinate system has Y=0 at
    /// bottom-left, so we flip: pdfY = pageHeight - layoutY.
    /// </summary>
    private static byte[] BuildContentStream(
        PositionedPage page,
        float pageHeightPt,
        Dictionary<string, FontObjectIds> fontObjMap)
    {
        var sb = new StringBuilder(4096);

        // Group elements by font family+size to minimise BT/ET pairs.
        // Emit BT … ET blocks, one per contiguous run of the same font.
        string? currentFamily = null;
        float currentSize = 0f;

        sb.AppendLine("BT");

        foreach (PositionedElement el in page.Elements)
        {
            if (el.Source is not InlineBox inline || string.IsNullOrEmpty(inline.Text))
                continue;

            // Switch font if needed (Tf operator)
            if (inline.FontFamily != currentFamily || inline.FontSize != currentSize)
            {
                currentFamily = inline.FontFamily;
                currentSize = inline.FontSize;

                string resName = fontObjMap.ContainsKey(currentFamily)
                    ? FontResourceName(currentFamily, fontObjMap)
                    : "F0"; // fallback — no font object available

                sb.Append('/');
                sb.Append(resName);
                sb.Append(' ');
                sb.Append(currentSize.ToString("F2", CultureInfo.InvariantCulture));
                sb.AppendLine(" Tf");
            }

            // Set color (rg operator — DeviceRGB)
            (float r, float g, float b) = ParseColor(inline.Color);
            sb.Append(r.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(g.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(b.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" rg");

            // Position (Td operator): x stays the same, y is flipped from top-origin to bottom-origin.
            float pdfX = el.Position.X;
            // Layout Y = distance from top of page. PDF Y = distance from bottom.
            // el.Position.Y is the top of the text box in layout space (top-origin).
            // We want the baseline. Approximate: pdfY = pageHeight - Y - fontSize (ascender rough approx).
            float pdfY = pageHeightPt - el.Position.Y - inline.FontSize;

            sb.Append(pdfX.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(pdfY.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" Td");

            // Text: emit as a literal string Tj (ONE operator for the entire run, not per word).
            // We use PDF literal string encoding: escape parentheses and backslash.
            sb.Append('(');
            AppendPdfString(sb, inline.Text);
            sb.AppendLine(") Tj");

            // Reset Td to absolute origin before next run to avoid cumulative offset drift.
            // We'll set absolute position each time using a fresh Td from (0,0).
            // Trick: emit a Td of (0,0) after each glyph run to reset the text matrix position
            // is wasteful; instead we track accumulated offset. Simpler: use Tm (text matrix)
            // to position absolutely each time.
            // Replace the Td approach with Tm for absolute positioning:
        }

        sb.AppendLine("ET");

        // Rebuild using Tm (absolute positioning) instead of Td (relative).
        // Clear and redo:
        sb.Clear();
        sb.AppendLine("BT");

        currentFamily = null;
        currentSize = 0f;

        foreach (PositionedElement el in page.Elements)
        {
            if (el.Source is not InlineBox inline || string.IsNullOrEmpty(inline.Text))
                continue;

            if (inline.FontFamily != currentFamily || inline.FontSize != currentSize)
            {
                currentFamily = inline.FontFamily;
                currentSize = inline.FontSize;

                string resName = fontObjMap.ContainsKey(currentFamily)
                    ? FontResourceName(currentFamily, fontObjMap)
                    : "F0";

                sb.Append('/');
                sb.Append(resName);
                sb.Append(' ');
                sb.Append(currentSize.ToString("F2", CultureInfo.InvariantCulture));
                sb.AppendLine(" Tf");
            }

            (float r, float g, float b) = ParseColor(inline.Color);
            sb.Append(r.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(g.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(b.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" rg");

            float pdfX = el.Position.X;
            float pdfY = pageHeightPt - el.Position.Y - inline.FontSize;

            // Tm: absolute text matrix (a b c d e f) — identity scale+rotate, just translate.
            sb.Append("1 0 0 1 ");
            sb.Append(pdfX.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(pdfY.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" Tm");

            sb.Append('(');
            AppendPdfString(sb, inline.Text);
            sb.AppendLine(") Tj");
        }

        sb.AppendLine("ET");

        return Encoding.Latin1.GetBytes(sb.ToString());
    }

    // ── helper: PDF literal string escaping ──────────────────────────────────

    private static void AppendPdfString(StringBuilder sb, string text)
    {
        foreach (char c in text)
        {
            switch (c)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                case '\r': sb.Append("\\r"); break;
                case '\n': sb.Append("\\n"); break;
                default:
                    // Latin1 range: encode directly. Characters outside Latin1 (>255) are
                    // replaced with '?' — full Unicode support requires CID fonts (out of
                    // scope for this spike).
                    if (c <= 0xFF)
                        sb.Append(c);
                    else
                        sb.Append('?');
                    break;
            }
        }
    }

    // ── helper: color ─────────────────────────────────────────────────────────

    private static (float R, float G, float B) ParseColor(string? cssColor)
    {
        if (string.IsNullOrEmpty(cssColor))
            return (0f, 0f, 0f); // black

        string c = cssColor.Trim().ToLowerInvariant();
        return c switch
        {
            "black" => (0f, 0f, 0f),
            "white" => (1f, 1f, 1f),
            "red" => (1f, 0f, 0f),
            "green" => (0f, 0.502f, 0f),
            "blue" => (0f, 0f, 1f),
            "gray" or "grey" => (0.502f, 0.502f, 0.502f),
            "yellow" => (1f, 1f, 0f),
            _ when c.Length == 7 && c[0] == '#' => ParseHexColor(c),
            _ => (0f, 0f, 0f)
        };
    }

    private static (float R, float G, float B) ParseHexColor(string c)
    {
        if (int.TryParse(c.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) &&
            int.TryParse(c.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) &&
            int.TryParse(c.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
        {
            return (r / 255f, g / 255f, b / 255f);
        }
        return (0f, 0f, 0f);
    }

    // ── helper: font resource name ───────────────────────────────────────────

    private static string FontResourceName(
        string family,
        Dictionary<string, FontObjectIds> fontObjMap)
    {
        int idx = 0;
        foreach (string key in fontObjMap.Keys)
        {
            if (key == family) return $"F{idx}";
            idx++;
        }
        return "F0";
    }

    // ── helper: font infos ───────────────────────────────────────────────────

    private static Dictionary<string, (EmbeddedFontInfo Info, Dictionary<int, ushort> CpToGid)> BuildFontInfos(
        PositionedPageList pageList)
    {
        var result = new Dictionary<string, (EmbeddedFontInfo, Dictionary<int, ushort>)>(StringComparer.Ordinal);
        foreach (EmbeddedFontInfo fi in pageList.EmbeddedFonts)
        {
            if (result.ContainsKey(fi.Family)) continue;
            result[fi.Family] = (fi, BuildCpToGidMap(fi.SubsetBytes.Span));
        }
        return result;
    }

    // Build a codepoint→GID map by parsing the cmap Format 4 subtable of the subset font.
    private static Dictionary<int, ushort> BuildCpToGidMap(ReadOnlySpan<byte> font)
    {
        var map = new Dictionary<int, ushort>();
        if (font.Length < 12) return map;

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(4, 2));
        int cmapOff = -1;
        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > font.Length) break;
            string tag = Encoding.ASCII.GetString(font.Slice(rec, 4));
            if (tag == "cmap")
            {
                cmapOff = (int)BinaryPrimitives.ReadUInt32BigEndian(font.Slice(rec + 8, 4));
                break;
            }
        }
        if (cmapOff < 0 || cmapOff + 4 > font.Length) return map;

        ushort numSubtables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(cmapOff + 2, 2));
        int fmt4Off = -1;
        for (int i = 0; i < numSubtables; i++)
        {
            int er = cmapOff + 4 + i * 8;
            if (er + 8 > font.Length) break;
            uint subOff = BinaryPrimitives.ReadUInt32BigEndian(font.Slice(er + 4, 4));
            int subAbs = cmapOff + (int)subOff;
            if (subAbs + 2 > font.Length) continue;
            ushort fmt = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(subAbs, 2));
            if (fmt == 4)
            {
                fmt4Off = subAbs;
                ushort pid = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(er, 2));
                ushort eid = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(er + 2, 2));
                if (pid == 3 && eid == 1) break;
            }
        }
        if (fmt4Off < 0) return map;

        int segCountX2 = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(fmt4Off + 6, 2));
        int segCount = segCountX2 / 2;
        int endOff = fmt4Off + 14;
        int startOff = endOff + segCountX2 + 2;
        int deltaOff = startOff + segCountX2;
        int rangeOff = deltaOff + segCountX2;

        for (int s = 0; s < segCount - 1; s++) // skip terminator
        {
            ushort end = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(endOff + s * 2, 2));
            ushort start = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(startOff + s * 2, 2));
            short delta = (short)BinaryPrimitives.ReadUInt16BigEndian(font.Slice(deltaOff + s * 2, 2));
            ushort range = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(rangeOff + s * 2, 2));

            for (int cp = start; cp <= end; cp++)
            {
                ushort gid;
                if (range == 0)
                {
                    gid = (ushort)((cp + delta) & 0xFFFF);
                }
                else
                {
                    int idx = rangeOff + s * 2 + range + (cp - start) * 2;
                    if (idx + 2 > font.Length) continue;
                    ushort raw = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(idx, 2));
                    gid = raw == 0 ? (ushort)0 : (ushort)((raw + delta) & 0xFFFF);
                }
                if (gid != 0) map[cp] = gid;
            }
        }
        return map;
    }

    // ── helper: widths array ─────────────────────────────────────────────────

    private static int[] BuildWidthsArray(EmbeddedFontInfo fi, out int firstChar, out int lastChar)
    {
        // Get the set of used codepoints that are in the Latin-1 range (0–255)
        // and build a Widths array from firstChar to lastChar.
        var latin1Cps = fi.UsedCodepoints.Where(cp => cp >= 0 && cp <= 255).OrderBy(cp => cp).ToList();

        if (latin1Cps.Count == 0)
        {
            firstChar = 32;
            lastChar = 32;
            return [1000]; // default width
        }

        firstChar = latin1Cps[0];
        lastChar = latin1Cps[^1];

        // Parse hmtx to get actual advances from the subset font
        var cpToGid = BuildCpToGidMap(fi.SubsetBytes.Span);
        var gidToAdvance = BuildGidToAdvanceMap(fi.SubsetBytes.Span);

        int count = lastChar - firstChar + 1;
        int[] widths = new int[count];
        for (int cp = firstChar; cp <= lastChar; cp++)
        {
            int advance = 1000; // default: 1000 units (= 1 em at 1000 upem)
            if (cpToGid.TryGetValue(cp, out ushort gid) && gidToAdvance.TryGetValue(gid, out int adv))
                advance = adv;
            widths[cp - firstChar] = advance;
        }
        return widths;
    }

    private static Dictionary<ushort, int> BuildGidToAdvanceMap(ReadOnlySpan<byte> font)
    {
        var map = new Dictionary<ushort, int>();
        if (font.Length < 12) return map;

        // Find hmtx and hhea
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(4, 2));
        int hmtxOff = -1, hheaOff = -1;
        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > font.Length) break;
            string tag = Encoding.ASCII.GetString(font.Slice(rec, 4));
            int off = (int)BinaryPrimitives.ReadUInt32BigEndian(font.Slice(rec + 8, 4));
            if (tag == "hmtx") hmtxOff = off;
            else if (tag == "hhea") hheaOff = off;
        }
        if (hmtxOff < 0 || hheaOff < 0) return map;

        int unitsPerEm = GetUnitsPerEm(font);
        int numberOfHMetrics = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(hheaOff + 34, 2));

        for (int i = 0; i < numberOfHMetrics; i++)
        {
            int off = hmtxOff + i * 4;
            if (off + 2 > font.Length) break;
            ushort rawAdv = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(off, 2));
            // Convert from font units to 1/1000 em (PDF widths at size 1)
            int pdfWidth = unitsPerEm > 0 ? (int)Math.Round(rawAdv * 1000.0 / unitsPerEm) : 1000;
            map[(ushort)i] = pdfWidth;
        }
        return map;
    }

    private static int GetUnitsPerEm(ReadOnlySpan<byte> font)
    {
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(4, 2));
        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > font.Length) break;
            string tag = Encoding.ASCII.GetString(font.Slice(rec, 4));
            if (tag == "head")
            {
                int off = (int)BinaryPrimitives.ReadUInt32BigEndian(font.Slice(rec + 8, 4));
                if (off + 20 > font.Length) break;
                return BinaryPrimitives.ReadUInt16BigEndian(font.Slice(off + 18, 2));
            }
        }
        return 1000;
    }

    // ── helper: PDF name sanitization ────────────────────────────────────────

    private static string PdfName(string family)
    {
        // Replace spaces and non-ASCII with hyphens for PDF name token safety.
        var sb = new StringBuilder(family.Length);
        foreach (char c in family)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '-');
        }
        return sb.ToString();
    }

    // ── helper: page dimensions ───────────────────────────────────────────────

    private static (float Width, float Height) GetPageDimensions(PdfRenderOptions options)
    {
        (float w, float h) = PdfPageSizeDimensions.Get(options.PageSize);
        return options.Orientation == PdfOrientation.Landscape ? (h, w) : (w, h);
    }

    // ── inner types ───────────────────────────────────────────────────────────

    private sealed record FontObjectIds(int FontDictId, int DescriptorId, int FontFileId);

    /// <summary>
    /// Accumulates PDF indirect objects, tracks byte offsets for the xref table, and
    /// finalizes the PDF with a cross-reference table and trailer.
    /// </summary>
    private sealed class PdfObjectStore
    {
        private int _nextId = 1;

        // (id → bytes) — stored in insertion order via List to preserve sequential IDs.
        private readonly List<(int Id, byte[] Data)> _objects = new();
        private readonly Dictionary<int, int> _idToSlot = new();

        public int ReserveId() => _nextId++;

        /// <summary>
        /// Writes an indirect object with the given ID. The callback receives a <see cref="PdfWriter"/>
        /// to accumulate the object body.
        /// </summary>
        public void WriteObject(int id, Action<PdfWriter> body)
        {
            var w = new PdfWriter();
            body(w);
            int slot = _objects.Count;
            _objects.Add((id, w.ToBytes()));
            _idToSlot[id] = slot;
        }

        /// <summary>
        /// Emits the complete PDF byte stream with header, objects, xref, and trailer.
        /// </summary>
        public byte[] Finalize(int rootId, int infoId)
        {
            // Sort objects by ID to produce a well-ordered file.
            _objects.Sort((a, b) => a.Id.CompareTo(b.Id));

            var ms = new MemoryStream(512 * 1024);

            // Header (PDF-1.7 + binary comment for transport safety)
            WriteAscii(ms, "%PDF-1.7\n%\xE2\xE3\xCF\xD3\n");

            // Track byte offsets for xref
            var offsets = new Dictionary<int, long>();

            foreach ((int id, byte[] data) in _objects)
            {
                offsets[id] = ms.Position;
                WriteAscii(ms, $"{id} 0 obj\n");
                ms.Write(data, 0, data.Length);
                WriteAscii(ms, "endobj\n");
            }

            // xref table
            long xrefPos = ms.Position;
            int maxId = _objects.Max(o => o.Id);
            WriteAscii(ms, "xref\n");
            WriteAscii(ms, $"0 {maxId + 1}\n");
            WriteAscii(ms, "0000000000 65535 f \n"); // free entry for object 0

            for (int i = 1; i <= maxId; i++)
            {
                if (offsets.TryGetValue(i, out long off))
                    WriteAscii(ms, $"{off:D10} 00000 n \n");
                else
                    WriteAscii(ms, "0000000000 65535 f \n"); // unused slot
            }

            // Trailer
            WriteAscii(ms, "trailer\n");
            WriteAscii(ms, $"<< /Size {maxId + 1} /Root {rootId} 0 R /Info {infoId} 0 R {FixedTrailerId} >>\n");
            WriteAscii(ms, "startxref\n");
            WriteAscii(ms, $"{xrefPos}\n");
            WriteAscii(ms, "%%EOF\n");

            return ms.ToArray();
        }

        private static void WriteAscii(Stream s, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            s.Write(bytes, 0, bytes.Length);
        }
    }

    /// <summary>
    /// Accumulates bytes for one PDF indirect object body.
    /// </summary>
    private sealed class PdfWriter
    {
        private readonly MemoryStream _ms = new();

        public void WriteRaw(string text)
        {
            byte[] bytes = Encoding.Latin1.GetBytes(text);
            _ms.Write(bytes, 0, bytes.Length);
        }

        public void WriteRawLine(string text)
        {
            WriteRaw(text);
            _ms.WriteByte((byte)'\n');
        }

        public void WriteBytes(byte[] data)
        {
            _ms.Write(data, 0, data.Length);
        }

        public byte[] ToBytes() => _ms.ToArray();
    }
}
