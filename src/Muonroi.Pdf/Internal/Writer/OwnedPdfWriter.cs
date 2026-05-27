// Phase 8.5 "Owned PDF Writer" — production implementation
//
// Security invariants (SEC-02):
// This writer NEVER emits /JavaScript, /Launch, /OpenAction, or /EmbeddedFile entries.
// The absence of such calls IS the enforcement.
//
// Determinism (DET-01/02/03):
// Fixed sentinel timestamp, fixed subset-prefix, fixed /ID — identical to PdfSharpCoreWriter.

using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Muonroi.Pdf.Abstractions.Exceptions;
using Muonroi.Pdf.Internal.Font;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Internal.Writer;

/// <summary>
/// Pure-managed PDF 1.7 writer. Zero PdfSharpCore types. Emits CID Type0 composite fonts
/// (Identity-H encoding, 2-byte GID content streams) for correct Unicode/Vietnamese rendering.
/// Fonts are embedded via the existing in-house <see cref="TrueTypeFontSubsetter"/>; images
/// are embedded as DCTDecode (JPEG) or FlateDecode raw-RGB (PNG) XObjects.
/// All content and font streams are FlateDecode-compressed via ZLibStream (RFC 1950).
/// </summary>
internal sealed class OwnedPdfWriter : IPdfWriter
{
    // ── determinism sentinels (DET-01/02/03) ─────────────────────────────────

    private const string FixedTrailerId =
        "/ID [<00000000000000000000000000000000><00000000000000000000000000000000>]";

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

        // Reserve CID font object IDs: Type0, CIDFont, FontDescriptor, FontFile2, ToUnicode = 5 per font
        // Use List for stable insertion-order (DET-01/02).
        var fontResources = new List<(string ResourceName, FontObjectIds Ids, EmbeddedFontInfo Info)>();
        {
            int idx = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (EmbeddedFontInfo fi in pageList.EmbeddedFonts)
            {
                if (!seen.Add(fi.Family)) continue;
                var ids = new FontObjectIds(
                    Type0Id: store.ReserveId(),
                    CIDFontId: store.ReserveId(),
                    DescriptorId: store.ReserveId(),
                    FontFileId: store.ReserveId(),
                    ToUnicodeId: store.ReserveId());
                fontResources.Add(($"F{idx}", ids, fi));
                idx++;
            }
        }

        // Reserve image XObject IDs: List for stable ordering.
        var imageResources = new List<(string ResourceName, int ObjectId, string Src)>();
        {
            int idx = 0;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, DecodedImage> kv in pageList.Images)
            {
                if (!seen.Add(kv.Key)) continue;
                imageResources.Add(($"Im{idx}", store.ReserveId(), kv.Key));
                idx++;
            }
        }

        // Build cp→newGid map per family (from OldToNewGid + cmap in subsetBytes)
        var cpToNewGidMap = BuildCpToNewGidMaps(fontResources);

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
                w.WriteRaw($" {pageObjIds[i]} 0 R");
            w.WriteRawLine($" ] /Count {pageCount} >>");
        });

        // Per-page objects
        for (int i = 0; i < pageCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            PositionedPage page = pageList.Pages[i];

            // Determine which fonts and images appear on this page
            var pageFonts = new List<(string ResourceName, FontObjectIds Ids)>();
            foreach (var (rn, ids, fi) in fontResources)
            {
                if (page.Elements.Any(e => e.Source is InlineBox ib && ib.FontFamily == fi.Family))
                    pageFonts.Add((rn, ids));
            }

            var pageImages = new List<(string ResourceName, int ObjectId)>();
            foreach (var (rn, oid, src) in imageResources)
            {
                if (page.Elements.Any(e => e.Source is ReplacedBox rb && rb.Src == src))
                    pageImages.Add((rn, oid));
            }

            byte[] rawContent = BuildContentStream(page, pageHeightPt, fontResources, imageResources, cpToNewGidMap);
            byte[] compressedContent = CompressFlateDecode(rawContent);

            // Content stream
            store.WriteObject(contentObjIds[i], w =>
            {
                w.WriteRawLine($"<< /Length {compressedContent.Length} /Filter /FlateDecode >>");
                w.WriteRawLine("stream");
                w.WriteBytes(compressedContent);
                w.WriteRawLine("\nendstream");
            });

            // Page dict
            store.WriteObject(pageObjIds[i], w =>
            {
                w.WriteRaw($"<< /Type /Page /Parent {pagesRootId} 0 R");
                w.WriteRaw($" /MediaBox [0 0 {pageWidthPt.ToString("F2", CultureInfo.InvariantCulture)} {pageHeightPt.ToString("F2", CultureInfo.InvariantCulture)}]");
                w.WriteRaw($" /Contents {contentObjIds[i]} 0 R");
                if (pageFonts.Count > 0 || pageImages.Count > 0)
                {
                    w.WriteRaw(" /Resources <<");
                    if (pageFonts.Count > 0)
                    {
                        w.WriteRaw(" /Font <<");
                        foreach ((string rn, FontObjectIds ids) in pageFonts)
                            w.WriteRaw($" /{rn} {ids.Type0Id} 0 R");
                        w.WriteRaw(" >>");
                    }
                    if (pageImages.Count > 0)
                    {
                        w.WriteRaw(" /XObject <<");
                        foreach ((string rn, int oid) in pageImages)
                            w.WriteRaw($" /{rn} {oid} 0 R");
                        w.WriteRaw(" >>");
                    }
                    w.WriteRaw(" >>");
                }
                w.WriteRawLine(" >>");
            });
        }

        // CID font objects (one set per embedded font)
        foreach ((string resName, FontObjectIds ids, EmbeddedFontInfo fi) in fontResources)
        {
            EmitCidFontObjects(store, ids, fi, cpToNewGidMap.TryGetValue(fi.Family, out var m) ? m : null);
        }

        // Image XObjects
        foreach ((string resName, int objectId, string src) in imageResources)
        {
            if (!pageList.Images.TryGetValue(src, out DecodedImage? image))
                continue;
            EmitImageXObject(store, objectId, image);
        }

        // Info dict (deterministic/sanitized)
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

    // ── CID font emission ──────────────────────────────────────────────────────

    private static void EmitCidFontObjects(
        PdfObjectStore store,
        FontObjectIds ids,
        EmbeddedFontInfo fi,
        Dictionary<int, ushort>? cpToNewGid)
    {
        byte[] subsetBytes = fi.SubsetBytes.ToArray();
        byte[] compressedFont = CompressFlateDecode(subsetBytes);
        string baseFontName = $"AAAAAA+{PdfName(fi.Family)}";

        // Read font metrics from subset bytes
        (int unitsPerEm, int ascent, int descent, int capHeight) = ReadFontMetrics(subsetBytes);

        // Build /W array from SortedGids + hmtx
        var gidToAdvance = BuildGidToAdvanceMap(subsetBytes, unitsPerEm);

        // ToUnicode CMap stream
        store.WriteObject(ids.ToUnicodeId, w =>
        {
            byte[] cmap = BuildToUnicodeCMap(fi, cpToNewGid);
            w.WriteRawLine($"<< /Length {cmap.Length} >>");
            w.WriteRawLine("stream");
            w.WriteBytes(cmap);
            w.WriteRawLine("\nendstream");
        });

        // FontFile2 stream (FlateDecode-compressed subset TTF)
        store.WriteObject(ids.FontFileId, w =>
        {
            w.WriteRawLine($"<< /Length {compressedFont.Length} /Length1 {subsetBytes.Length} /Filter /FlateDecode >>");
            w.WriteRawLine("stream");
            w.WriteBytes(compressedFont);
            w.WriteRawLine("\nendstream");
        });

        // FontDescriptor
        int lly = (int)Math.Round(descent * 1000.0 / unitsPerEm);
        int ury = (int)Math.Round(ascent * 1000.0 / unitsPerEm);
        int pdfAscent = ury;
        int pdfDescent = lly;
        int pdfCapHeight = (int)Math.Round(capHeight * 1000.0 / unitsPerEm);

        store.WriteObject(ids.DescriptorId, w =>
        {
            w.WriteRaw($"<< /Type /FontDescriptor /FontName /{baseFontName}");
            w.WriteRaw(" /Flags 4");
            w.WriteRaw($" /FontBBox [0 {pdfDescent} 1000 {pdfAscent}]");
            w.WriteRaw(" /ItalicAngle 0");
            w.WriteRaw($" /Ascent {pdfAscent}");
            w.WriteRaw($" /Descent {pdfDescent}");
            w.WriteRaw($" /CapHeight {(pdfCapHeight > 0 ? pdfCapHeight : 700)}");
            w.WriteRaw(" /StemV 80");
            w.WriteRaw($" /FontFile2 {ids.FontFileId} 0 R");
            w.WriteRawLine(" >>");
        });

        // CIDFont (descendant)
        store.WriteObject(ids.CIDFontId, w =>
        {
            w.WriteRaw($"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{baseFontName}");
            w.WriteRaw(" /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>");
            w.WriteRaw(" /DW 1000");
            // /W array: sparse format gid [width] for each glyph in SortedGids
            if (fi.SortedGids.Count > 0)
            {
                w.WriteRaw(" /W [");
                foreach (ushort newGid in fi.SortedGids)
                {
                    int width = gidToAdvance.TryGetValue(newGid, out int adv) ? adv : 1000;
                    w.WriteRaw($" {newGid} [{width}]");
                }
                w.WriteRaw(" ]");
            }
            w.WriteRaw($" /FontDescriptor {ids.DescriptorId} 0 R");
            w.WriteRaw(" /CIDToGIDMap /Identity");
            w.WriteRawLine(" >>");
        });

        // Type0 font (top-level)
        store.WriteObject(ids.Type0Id, w =>
        {
            w.WriteRaw($"<< /Type /Font /Subtype /Type0 /BaseFont /{baseFontName}");
            w.WriteRaw(" /Encoding /Identity-H");
            w.WriteRaw($" /DescendantFonts [{ids.CIDFontId} 0 R]");
            w.WriteRaw($" /ToUnicode {ids.ToUnicodeId} 0 R");
            w.WriteRawLine(" >>");
        });
    }

    private static byte[] BuildToUnicodeCMap(
        EmbeddedFontInfo fi,
        Dictionary<int, ushort>? cpToNewGid)
    {
        // Build bfchar entries: newGid → Unicode codepoint
        var entries = new List<(ushort NewGid, int Cp)>();

        if (cpToNewGid != null && fi.OldToNewGid.Count > 0)
        {
            // Build reverse map: newGid → cp (via oldGid → newGid and cp → oldGid chain)
            // cpToNewGid is cp → newGid (already composed through OldToNewGid)
            var newGidToCp = new Dictionary<ushort, int>();
            foreach ((int cp, ushort newGid) in cpToNewGid)
            {
                if (!newGidToCp.ContainsKey(newGid))
                    newGidToCp[newGid] = cp;
            }

            foreach (ushort newGid in fi.SortedGids)
            {
                if (newGidToCp.TryGetValue(newGid, out int cp))
                    entries.Add((newGid, cp));
            }
        }
        else if (cpToNewGid != null)
        {
            // Fallback: just use cpToNewGid directly
            var newGidToCp = new Dictionary<ushort, int>();
            foreach ((int cp, ushort newGid) in cpToNewGid)
            {
                if (!newGidToCp.ContainsKey(newGid))
                    newGidToCp[newGid] = cp;
            }
            foreach ((ushort newGid, int cp) in newGidToCp)
                entries.Add((newGid, cp));
            entries.Sort((a, b) => a.NewGid.CompareTo(b.NewGid));
        }

        var sb = new StringBuilder();
        sb.AppendLine("/CIDInit /ProcSet findresource begin");
        sb.AppendLine("12 dict begin");
        sb.AppendLine("begincmap");
        sb.AppendLine("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def");
        sb.AppendLine("/CMapName /Adobe-Identity-UCS def");
        sb.AppendLine("/CMapType 2 def");
        sb.AppendLine("1 begincodespacerange");
        sb.AppendLine("<0000> <FFFF>");
        sb.AppendLine("endcodespacerange");

        if (entries.Count > 0)
        {
            sb.AppendLine($"{entries.Count} beginbfchar");
            foreach ((ushort newGid, int cp) in entries)
            {
                // For BMP codepoints (Vietnamese is all BMP)
                sb.AppendLine($"<{newGid:X4}> <{cp:X4}>");
            }
            sb.AppendLine("endbfchar");
        }

        sb.AppendLine("endcmap");
        sb.AppendLine("CMapName currentdict /CMap defineresource pop");
        sb.AppendLine("end");
        sb.AppendLine("end");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    // ── Image XObject emission ────────────────────────────────────────────────

    private static void EmitImageXObject(PdfObjectStore store, int objectId, DecodedImage image)
    {
        byte[] data = image.Data.ToArray();
        bool isJpeg = image.ContentType == "image/jpeg" ||
                      (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xD8);
        bool isPng = image.ContentType == "image/png" ||
                     (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47);

        if (isJpeg)
        {
            // JPEG: passthrough as /DCTDecode
            store.WriteObject(objectId, w =>
            {
                w.WriteRaw($"<< /Type /XObject /Subtype /Image");
                w.WriteRaw($" /Width {image.Width} /Height {image.Height}");
                w.WriteRaw(" /ColorSpace /DeviceRGB /BitsPerComponent 8");
                w.WriteRaw($" /Filter /DCTDecode /Length {data.Length}");
                w.WriteRawLine(" >>");
                w.WriteRawLine("stream");
                w.WriteBytes(data);
                w.WriteRawLine("\nendstream");
            });
        }
        else if (isPng)
        {
            // PNG: decode IDAT, un-filter, re-encode as FlateDecode raw RGB
            byte[] rawRgb = DecodePngToRawRgb(data, image.Width, image.Height);
            byte[] compressed = CompressFlateDecode(rawRgb);

            store.WriteObject(objectId, w =>
            {
                w.WriteRaw($"<< /Type /XObject /Subtype /Image");
                w.WriteRaw($" /Width {image.Width} /Height {image.Height}");
                w.WriteRaw(" /ColorSpace /DeviceRGB /BitsPerComponent 8");
                w.WriteRaw($" /Filter /FlateDecode /Length {compressed.Length}");
                w.WriteRawLine(" >>");
                w.WriteRawLine("stream");
                w.WriteBytes(compressed);
                w.WriteRawLine("\nendstream");
            });
        }
        else
        {
            throw new PdfFormatException("IMAGE-FORMAT",
                $"Unsupported image format: {image.ContentType}");
        }
    }

    private static byte[] DecodePngToRawRgb(byte[] pngData, int width, int height)
    {
        // Validate PNG magic
        if (pngData.Length < 33)
            throw new PdfFormatException("IMAGE-FORMAT", "PNG too short to parse IHDR");

        // IHDR chunk: offset 16, length 13
        // Byte 24 = bit_depth, byte 25 = color_type
        byte bitDepth = pngData[24];
        byte colorType = pngData[25];

        if (colorType != 2 || bitDepth != 8)
            throw new PdfFormatException("IMAGE-FORMAT",
                $"Unsupported PNG: color_type={colorType} bit_depth={bitDepth}. Only 8-bit RGB PNG is supported.");

        // Collect all IDAT chunk payloads by scanning chunks
        var idatPayload = new List<byte>();
        int pos = 8; // skip PNG magic signature
        while (pos + 8 <= pngData.Length)
        {
            int chunkLen = (int)BinaryPrimitives.ReadUInt32BigEndian(pngData.AsSpan(pos, 4));
            string chunkType = Encoding.ASCII.GetString(pngData, pos + 4, 4);
            if (chunkType == "IDAT")
            {
                int dataStart = pos + 8;
                int dataEnd = dataStart + chunkLen;
                if (dataEnd > pngData.Length) break;
                for (int i = dataStart; i < dataEnd; i++)
                    idatPayload.Add(pngData[i]);
            }
            else if (chunkType == "IEND")
                break;
            pos += 8 + chunkLen + 4; // length + type + data + CRC
        }

        if (idatPayload.Count == 0)
            throw new PdfFormatException("IMAGE-FORMAT", "PNG has no IDAT chunks");

        // Inflate IDAT payload: it is a zlib/RFC1950 datastream — use ZLibStream NOT DeflateStream
        byte[] idatBytes = idatPayload.ToArray();
        byte[] filteredScanlines;
        using (var idatStream = new MemoryStream(idatBytes))
        using (var zlib = new ZLibStream(idatStream, CompressionMode.Decompress))
        using (var outMs = new MemoryStream())
        {
            zlib.CopyTo(outMs);
            filteredScanlines = outMs.ToArray();
        }

        // Un-filter rows: each row is (1 filter byte) + (width * 3 bytes for RGB)
        int bytesPerRow = width * 3;
        int rowStride = bytesPerRow + 1; // +1 for filter byte
        byte[] rawPixels = new byte[height * bytesPerRow];

        byte[] prevRow = new byte[bytesPerRow]; // initialized to zeros

        for (int row = 0; row < height; row++)
        {
            int srcRowStart = row * rowStride;
            if (srcRowStart >= filteredScanlines.Length) break;

            byte filterType = filteredScanlines[srcRowStart];
            int dstRowStart = row * bytesPerRow;

            // Copy raw filtered data for this row
            int copyLen = Math.Min(bytesPerRow, filteredScanlines.Length - srcRowStart - 1);
            for (int x = 0; x < copyLen; x++)
                rawPixels[dstRowStart + x] = filteredScanlines[srcRowStart + 1 + x];

            // Apply reverse filter
            byte[] curRow = rawPixels.AsSpan(dstRowStart, bytesPerRow).ToArray();
            ApplyPngUnFilter(filterType, curRow, prevRow, bytesPerRow, 3);
            curRow.CopyTo(rawPixels, dstRowStart);

            prevRow = curRow;
        }

        return rawPixels;
    }

    private static void ApplyPngUnFilter(byte filterType, byte[] row, byte[] prev, int rowLen, int bpp)
    {
        switch (filterType)
        {
            case 0: // None
                break;
            case 1: // Sub
                for (int x = bpp; x < rowLen; x++)
                    row[x] = (byte)(row[x] + row[x - bpp]);
                break;
            case 2: // Up
                for (int x = 0; x < rowLen; x++)
                    row[x] = (byte)(row[x] + prev[x]);
                break;
            case 3: // Average
                for (int x = 0; x < rowLen; x++)
                {
                    int a = x >= bpp ? row[x - bpp] : 0;
                    int b = prev[x];
                    row[x] = (byte)(row[x] + (a + b) / 2);
                }
                break;
            case 4: // Paeth
                for (int x = 0; x < rowLen; x++)
                {
                    int a = x >= bpp ? row[x - bpp] : 0;
                    int b = prev[x];
                    int c = x >= bpp ? prev[x - bpp] : 0;
                    row[x] = (byte)(row[x] + PaethPredictor(a, b, c));
                }
                break;
            default:
                throw new PdfFormatException("IMAGE-FORMAT",
                    $"Unsupported PNG filter type: {filterType}. Only filter types 0-4 are supported.");
        }
    }

    private static int PaethPredictor(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        if (pb <= pc) return b;
        return c;
    }

    // ── content stream builder ────────────────────────────────────────────────

    private static byte[] BuildContentStream(
        PositionedPage page,
        float pageHeightPt,
        List<(string ResourceName, FontObjectIds Ids, EmbeddedFontInfo Info)> fontResources,
        List<(string ResourceName, int ObjectId, string Src)> imageResources,
        Dictionary<string, Dictionary<int, ushort>> cpToNewGidMap)
    {
        // Build family → resourceName map for fast lookup
        var familyToResName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string rn, _, EmbeddedFontInfo fi) in fontResources)
            familyToResName[fi.Family] = rn;

        // Build src → resourceName map for images
        var srcToResName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string rn, _, string src) in imageResources)
            srcToResName[src] = rn;

        var sb = new StringBuilder(4096);
        sb.AppendLine("BT");

        string? currentFamily = null;
        float currentSize = 0f;

        foreach (PositionedElement el in page.Elements)
        {
            if (el.Source is ReplacedBox replaced)
            {
                // Image — handled outside BT/ET block; close text block temporarily
                // Flush any open BT block to handle image insertion
                sb.AppendLine("ET");

                // Image CTM + Do operator
                if (srcToResName.TryGetValue(replaced.Src ?? "", out string? imgRes))
                {
                    float pdfX = el.Position.X;
                    float pdfY = pageHeightPt - el.Position.Y - el.Position.Height;
                    float w = el.Position.Width;
                    float h = el.Position.Height;

                    sb.AppendLine("q");
                    sb.AppendLine($"{w.ToString("F4", CultureInfo.InvariantCulture)} 0 0 {h.ToString("F4", CultureInfo.InvariantCulture)} {pdfX.ToString("F4", CultureInfo.InvariantCulture)} {pdfY.ToString("F4", CultureInfo.InvariantCulture)} cm");
                    sb.AppendLine($"/{imgRes} Do");
                    sb.AppendLine("Q");
                }

                sb.AppendLine("BT");
                currentFamily = null; // force Tf re-emit after image
                currentSize = 0f;
                continue;
            }

            if (el.Source is not InlineBox inline || string.IsNullOrEmpty(inline.Text))
                continue;

            // Switch font if needed
            if (inline.FontFamily != currentFamily || inline.FontSize != currentSize)
            {
                currentFamily = inline.FontFamily;
                currentSize = inline.FontSize;

                string resName = familyToResName.TryGetValue(currentFamily, out string? rn) ? rn : "F0";
                sb.Append('/');
                sb.Append(resName);
                sb.Append(' ');
                sb.Append(currentSize.ToString("F2", CultureInfo.InvariantCulture));
                sb.AppendLine(" Tf");
            }

            // Color
            (float r, float g, float b) = ParseColor(inline.Color);
            sb.Append(r.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(g.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(b.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" rg");

            // Absolute positioning via Tm
            float pdfXt = el.Position.X;
            float pdfYt = pageHeightPt - el.Position.Y - inline.FontSize;
            sb.Append("1 0 0 1 ");
            sb.Append(pdfXt.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(pdfYt.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" Tm");

            // Text as 2-byte GID hex string using CID encoding
            if (cpToNewGidMap.TryGetValue(inline.FontFamily, out Dictionary<int, ushort>? cpMap) && cpMap.Count > 0)
            {
                sb.Append('<');
                foreach (char c in inline.Text)
                {
                    if (cpMap.TryGetValue((int)c, out ushort newGid))
                        sb.Append(newGid.ToString("X4"));
                    else
                        sb.Append("0000"); // .notdef for unmapped glyphs
                }
                sb.AppendLine("> Tj");
            }
            else
            {
                // Fallback: emit as literal string (Latin-1, for fonts with empty cpMap)
                sb.Append('(');
                AppendPdfStringLatin1(sb, inline.Text);
                sb.AppendLine(") Tj");
            }
        }

        sb.AppendLine("ET");

        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    // ── helper: FlateDecode compression ───────────────────────────────────────

    private static byte[] CompressFlateDecode(byte[] data,
        CompressionLevel level = CompressionLevel.Optimal)
    {
        using var output = new MemoryStream(data.Length / 2 + 64);
        using (var zlib = new ZLibStream(output, level, leaveOpen: true))
            zlib.Write(data, 0, data.Length);
        return output.ToArray();
    }

    // ── helper: cp → newGid maps ─────────────────────────────────────────────

    private static Dictionary<string, Dictionary<int, ushort>> BuildCpToNewGidMaps(
        List<(string ResourceName, FontObjectIds Ids, EmbeddedFontInfo Info)> fontResources)
    {
        var result = new Dictionary<string, Dictionary<int, ushort>>(StringComparer.Ordinal);
        foreach ((_, _, EmbeddedFontInfo fi) in fontResources)
        {
            if (result.ContainsKey(fi.Family)) continue;

            if (fi.OldToNewGid.Count > 0)
            {
                // Parse cmap of subset font to get cp→oldGid, then compose with oldGid→newGid
                var cpToOldGid = BuildCpToOldGidMap(fi.SubsetBytes.Span, fi.OldToNewGid);
                var cpToNewGid = new Dictionary<int, ushort>(cpToOldGid.Count);
                foreach ((int cp, ushort oldGid) in cpToOldGid)
                {
                    if (fi.OldToNewGid.TryGetValue(oldGid, out ushort newGid))
                        cpToNewGid[cp] = newGid;
                }
                result[fi.Family] = cpToNewGid;
            }
            else
            {
                // Fallback: parse subset cmap and treat GIDs as new GIDs (identity mapping)
                var cpToGid = BuildCpToGidFromSubset(fi.SubsetBytes.Span);
                result[fi.Family] = cpToGid;
            }
        }
        return result;
    }

    // Parse the subset font's cmap to get cp → old GID mapping.
    // We pass in the OldToNewGid so we can validate bounds.
    private static Dictionary<int, ushort> BuildCpToOldGidMap(
        ReadOnlySpan<byte> font,
        IReadOnlyDictionary<ushort, ushort> oldToNewGid)
    {
        var map = new Dictionary<int, ushort>();
        if (font.Length < 12) return map;

        int cmapOff = FindCmapOffset(font);
        if (cmapOff < 0) return map;

        int fmt4Off = FindFormat4Subtable(font, cmapOff);
        if (fmt4Off < 0) return map;

        ParseFormat4ToMap(font, fmt4Off, map);
        return map;
    }

    private static Dictionary<int, ushort> BuildCpToGidFromSubset(ReadOnlySpan<byte> font)
    {
        var map = new Dictionary<int, ushort>();
        if (font.Length < 12) return map;

        int cmapOff = FindCmapOffset(font);
        if (cmapOff < 0) return map;

        int fmt4Off = FindFormat4Subtable(font, cmapOff);
        if (fmt4Off < 0) return map;

        ParseFormat4ToMap(font, fmt4Off, map);
        return map;
    }

    private static int FindCmapOffset(ReadOnlySpan<byte> font)
    {
        if (font.Length < 12) return -1;
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(4, 2));
        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > font.Length) break;
            string tag = Encoding.ASCII.GetString(font.Slice(rec, 4));
            if (tag == "cmap")
                return (int)BinaryPrimitives.ReadUInt32BigEndian(font.Slice(rec + 8, 4));
        }
        return -1;
    }

    private static int FindFormat4Subtable(ReadOnlySpan<byte> font, int cmapBase)
    {
        if (cmapBase + 4 > font.Length) return -1;
        ushort numSubtables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(cmapBase + 2, 2));
        int fmt4Off = -1;
        for (int i = 0; i < numSubtables; i++)
        {
            int er = cmapBase + 4 + i * 8;
            if (er + 8 > font.Length) break;
            uint subOff = BinaryPrimitives.ReadUInt32BigEndian(font.Slice(er + 4, 4));
            int subAbs = cmapBase + (int)subOff;
            if (subAbs + 2 > font.Length) continue;
            ushort fmt = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(subAbs, 2));
            if (fmt == 4)
            {
                fmt4Off = subAbs;
                ushort pid = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(er, 2));
                ushort eid = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(er + 2, 2));
                if (pid == 3 && eid == 1) break; // prefer Windows BMP
            }
        }
        return fmt4Off;
    }

    private static void ParseFormat4ToMap(ReadOnlySpan<byte> font, int fmt4Off, Dictionary<int, ushort> map)
    {
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
                    gid = (ushort)((cp + delta) & 0xFFFF);
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
    }

    // ── helper: font metrics ─────────────────────────────────────────────────

    private static (int UnitsPerEm, int Ascent, int Descent, int CapHeight) ReadFontMetrics(byte[] font)
    {
        int unitsPerEm = 1000;
        int ascent = 800;
        int descent = -200;
        int capHeight = 700;

        if (font.Length < 12) return (unitsPerEm, ascent, descent, capHeight);

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4, 2));
        int headOff = -1, os2Off = -1;

        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > font.Length) break;
            string tag = Encoding.ASCII.GetString(font, rec, 4);
            int off = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(rec + 8, 4));
            if (tag == "head") headOff = off;
            else if (tag == "OS/2") os2Off = off;
        }

        if (headOff >= 0 && headOff + 20 <= font.Length)
            unitsPerEm = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(headOff + 18, 2));

        if (os2Off >= 0 && os2Off + 90 <= font.Length)
        {
            ascent = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(os2Off + 68, 2));   // sTypoAscender at offset 68
            descent = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(os2Off + 70, 2));  // sTypoDescender at offset 70
            capHeight = BinaryPrimitives.ReadInt16BigEndian(font.AsSpan(os2Off + 88, 2)); // sCapHeight at offset 88
        }

        return (unitsPerEm, ascent, descent, capHeight);
    }

    // ── helper: gid → advance width ─────────────────────────────────────────

    private static Dictionary<ushort, int> BuildGidToAdvanceMap(byte[] font, int unitsPerEm)
    {
        var map = new Dictionary<ushort, int>();
        if (font.Length < 12) return map;

        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(4, 2));
        int hmtxOff = -1, hheaOff = -1;
        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > font.Length) break;
            string tag = Encoding.ASCII.GetString(font, rec, 4);
            int off = (int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(rec + 8, 4));
            if (tag == "hmtx") hmtxOff = off;
            else if (tag == "hhea") hheaOff = off;
        }
        if (hmtxOff < 0 || hheaOff < 0) return map;

        int numberOfHMetrics = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(hheaOff + 34, 2));
        for (int i = 0; i < numberOfHMetrics; i++)
        {
            int off = hmtxOff + i * 4;
            if (off + 2 > font.Length) break;
            ushort rawAdv = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(off, 2));
            int pdfWidth = unitsPerEm > 0 ? (int)Math.Round(rawAdv * 1000.0 / unitsPerEm) : 1000;
            map[(ushort)i] = pdfWidth;
        }
        return map;
    }

    // ── helper: PDF literal string (Latin-1 fallback only) ──────────────────

    private static void AppendPdfStringLatin1(StringBuilder sb, string text)
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
                    sb.Append(c <= 0xFF ? c : '?');
                    break;
            }
        }
    }

    // ── helper: color ─────────────────────────────────────────────────────────

    private static (float R, float G, float B) ParseColor(string? cssColor)
    {
        if (string.IsNullOrEmpty(cssColor)) return (0f, 0f, 0f);
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
            return (r / 255f, g / 255f, b / 255f);
        return (0f, 0f, 0f);
    }

    // ── helper: PDF name sanitization ────────────────────────────────────────

    private static string PdfName(string family)
    {
        var sb = new StringBuilder(family.Length);
        foreach (char c in family)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' ? c : '-');
        return sb.ToString();
    }

    // ── helper: page dimensions ───────────────────────────────────────────────

    private static (float Width, float Height) GetPageDimensions(PdfRenderOptions options)
    {
        (float w, float h) = PdfPageSizeDimensions.Get(options.PageSize);
        return options.Orientation == PdfOrientation.Landscape ? (h, w) : (w, h);
    }

    // ── inner types ───────────────────────────────────────────────────────────

    private sealed record FontObjectIds(
        int Type0Id,
        int CIDFontId,
        int DescriptorId,
        int FontFileId,
        int ToUnicodeId);

    /// <summary>
    /// Accumulates PDF indirect objects, tracks byte offsets for the xref table, and
    /// finalizes the PDF with a cross-reference table and trailer.
    /// </summary>
    private sealed class PdfObjectStore
    {
        private int _nextId = 1;

        private readonly List<(int Id, byte[] Data)> _objects = new();

        public int ReserveId() => _nextId++;

        public void WriteObject(int id, Action<PdfWriter> body)
        {
            var w = new PdfWriter();
            body(w);
            _objects.Add((id, w.ToBytes()));
        }

        public byte[] Finalize(int rootId, int infoId)
        {
            _objects.Sort((a, b) => a.Id.CompareTo(b.Id));

            var ms = new MemoryStream(512 * 1024);

            // Header (PDF-1.7 + binary comment for transport safety)
            WriteAscii(ms, "%PDF-1.7\n");
            ms.WriteByte(0x25); // %
            ms.WriteByte(0xE2);
            ms.WriteByte(0xE3);
            ms.WriteByte(0xCF);
            ms.WriteByte(0xD3);
            ms.WriteByte(0x0A); // \n

            var offsets = new Dictionary<int, long>();

            foreach ((int id, byte[] data) in _objects)
            {
                offsets[id] = ms.Position;
                WriteAscii(ms, $"{id} 0 obj\n");
                ms.Write(data, 0, data.Length);
                WriteAscii(ms, "endobj\n");
            }

            long xrefPos = ms.Position;
            int maxId = _objects.Max(o => o.Id);
            WriteAscii(ms, "xref\n");
            WriteAscii(ms, $"0 {maxId + 1}\n");
            WriteAscii(ms, "0000000000 65535 f \n");

            for (int i = 1; i <= maxId; i++)
            {
                if (offsets.TryGetValue(i, out long off))
                    WriteAscii(ms, $"{off:D10} 00000 n \n");
                else
                    WriteAscii(ms, "0000000000 65535 f \n");
            }

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

        public void WriteBytes(byte[] data) => _ms.Write(data, 0, data.Length);

        public byte[] ToBytes() => _ms.ToArray();
    }
}
