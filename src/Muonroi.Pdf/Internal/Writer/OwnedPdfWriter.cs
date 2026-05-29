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

        // Build cp→newGid map per family from the authoritative mapping threaded out of the subsetter.
        // This map was computed at subsetting time (BuildCmapTable) and is correct by construction —
        // no post-hoc cmap parse needed or attempted.
        var cpToNewGidMap = new Dictionary<string, Dictionary<int, ushort>>(StringComparer.Ordinal);
        foreach ((_, _, EmbeddedFontInfo fi) in fontResources)
        {
            if (!cpToNewGidMap.ContainsKey(fi.Family))
                cpToNewGidMap[fi.Family] = new Dictionary<int, ushort>(fi.CpToNewGid);
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
                if (page.Elements.Any(e =>
                        (e.Source is ReplacedBox rb && rb.Src == src) ||
                        (e.Source?.BackgroundImageSrc == src)))
                    pageImages.Add((rn, oid));
            }

            // Reserve annotation object IDs for this page's link annotations
            int[] annotIds = page.LinkAnnotations
                .Select(_ => store.ReserveId())
                .ToArray();

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
                // /Annots array for link annotations (SEC-02: only /S /URI action, no JS/Launch)
                if (annotIds.Length > 0)
                {
                    w.WriteRaw(" /Annots [");
                    foreach (int annotId in annotIds)
                        w.WriteRaw($" {annotId} 0 R");
                    w.WriteRaw(" ]");
                }
                w.WriteRawLine(" >>");
            });

            // Emit annotation indirect objects for this page
            for (int j = 0; j < page.LinkAnnotations.Count; j++)
            {
                LinkAnnotation annot = page.LinkAnnotations[j];
                int annotObjId = annotIds[j];

                // Defense-in-depth: double-check href does not start with javascript:
                if (annot.Href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                    continue; // silently skip — policy layer already filtered, this is second-layer

                // Y-flip: layout Y=0 at top → PDF Y=0 at bottom
                float llx = annot.X;
                float lly = pageHeightPt - annot.Y - annot.Height;
                float urx = annot.X + annot.Width;
                float ury = pageHeightPt - annot.Y;

                store.WriteObject(annotObjId, w =>
                {
                    w.WriteRaw("<< /Type /Annot");
                    w.WriteRaw(" /Subtype /Link");
                    w.WriteRaw($" /Rect [{llx.ToString("F4", CultureInfo.InvariantCulture)} {lly.ToString("F4", CultureInfo.InvariantCulture)} {urx.ToString("F4", CultureInfo.InvariantCulture)} {ury.ToString("F4", CultureInfo.InvariantCulture)}]");
                    w.WriteRaw(" /Border [0 0 0]");
                    w.WriteRaw($" /A << /S /URI /URI ({EscapePdfString(annot.Href)}) >>");
                    w.WriteRawLine(" >>");
                });
            }
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
            // /W array: sparse format newGid [width].
            // fi.SortedGids contains OLD (pre-subset) GIDs; after subsetting they are renumbered
            // to sequential new GIDs 0..N-1 stored in fi.OldToNewGid.  The rebuilt hmtx (and
            // therefore gidToAdvance) is indexed by the NEW sequential GID, not the old one.
            // Emitting oldGid as the CID would reference nonexistent or wrong glyphs — Bug B.
            if (fi.SortedGids.Count > 0)
            {
                w.WriteRaw(" /W [");
                foreach (ushort oldGid in fi.SortedGids)
                {
                    if (!fi.OldToNewGid.TryGetValue(oldGid, out ushort newGid))
                        continue; // glyph not in subset (should not happen, but skip safely)
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

        if (cpToNewGid != null && cpToNewGid.Count > 0)
        {
            // Build reverse map newGid → cp from the authoritative cp→newGid mapping.
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

        if (colorType != 2 && colorType != 3 && colorType != 6)
            throw new PdfFormatException("IMAGE-FORMAT",
                $"Unsupported PNG: color_type={colorType} bit_depth={bitDepth}. Supported: 8-bit RGB (type 2), palette (type 3), RGBA (type 6).");

        // Scan all chunks: collect IDAT payload and (for palette) PLTE + tRNS chunks.
        var idatPayload = new List<byte>();
        byte[]? plteData = null;   // PLTE: 3 bytes per entry (R, G, B), up to 256 entries
        byte[]? trnsData = null;   // tRNS for palette: 1 byte per entry (alpha), partial arrays allowed

        int pos = 8; // skip PNG magic signature
        while (pos + 8 <= pngData.Length)
        {
            int chunkLen = (int)BinaryPrimitives.ReadUInt32BigEndian(pngData.AsSpan(pos, 4));
            string chunkType = Encoding.ASCII.GetString(pngData, pos + 4, 4);
            int dataStart = pos + 8;
            int dataEnd = dataStart + chunkLen;
            if (dataEnd > pngData.Length) break;

            switch (chunkType)
            {
                case "PLTE":
                    plteData = pngData[dataStart..dataEnd];
                    break;
                case "tRNS":
                    trnsData = pngData[dataStart..dataEnd];
                    break;
                case "IDAT":
                    for (int i = dataStart; i < dataEnd; i++)
                        idatPayload.Add(pngData[i]);
                    break;
                case "IEND":
                    goto doneChunks;
            }

            pos += 8 + chunkLen + 4; // length + type + data + CRC
        }

        doneChunks:

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

        return colorType switch
        {
            2 => DecodePngRgb(filteredScanlines, width, height),
            3 => DecodePngPalette(filteredScanlines, width, height, plteData, trnsData),
            6 => DecodePngRgba(filteredScanlines, width, height),
            _ => throw new PdfFormatException("IMAGE-FORMAT", $"Unreachable color_type={colorType}")
        };
    }

    /// <summary>
    /// Decode 8-bit RGB (color_type=2) PNG scanlines to raw RGB pixel buffer.
    /// </summary>
    private static byte[] DecodePngRgb(byte[] filteredScanlines, int width, int height)
    {
        int bytesPerRow = width * 3;
        int rowStride = bytesPerRow + 1; // +1 for filter byte
        byte[] rawPixels = new byte[height * bytesPerRow];
        byte[] prevRow = new byte[bytesPerRow];

        for (int row = 0; row < height; row++)
        {
            int srcRowStart = row * rowStride;
            if (srcRowStart >= filteredScanlines.Length) break;

            byte filterType = filteredScanlines[srcRowStart];
            int dstRowStart = row * bytesPerRow;

            int copyLen = Math.Min(bytesPerRow, filteredScanlines.Length - srcRowStart - 1);
            for (int x = 0; x < copyLen; x++)
                rawPixels[dstRowStart + x] = filteredScanlines[srcRowStart + 1 + x];

            byte[] curRow = rawPixels.AsSpan(dstRowStart, bytesPerRow).ToArray();
            ApplyPngUnFilter(filterType, curRow, prevRow, bytesPerRow, 3);
            curRow.CopyTo(rawPixels, dstRowStart);
            prevRow = curRow;
        }

        return rawPixels;
    }

    /// <summary>
    /// Decode 8-bit palette/indexed (color_type=3) PNG scanlines to raw RGB pixel buffer.
    /// Each scanline byte is an index into the PLTE table.
    /// If a tRNS chunk is present, pixels with alpha &lt; 255 are composited onto a white background
    /// using the alpha-over formula: out = (alpha/255)*color + (1 - alpha/255)*255.
    /// </summary>
    private static byte[] DecodePngPalette(
        byte[] filteredScanlines,
        int width,
        int height,
        byte[]? plteData,
        byte[]? trnsData)
    {
        if (plteData == null || plteData.Length < 3)
            throw new PdfFormatException("IMAGE-FORMAT", "Palette PNG (color_type=3) has no PLTE chunk.");

        int paletteCount = plteData.Length / 3;
        int rowStride = width + 1; // 1 filter byte + 1 byte per pixel (index)
        byte[] rawPixels = new byte[height * width * 3];
        byte[] prevRow = new byte[width]; // index-domain prev row for un-filtering

        for (int row = 0; row < height; row++)
        {
            int srcRowStart = row * rowStride;
            if (srcRowStart >= filteredScanlines.Length) break;

            byte filterType = filteredScanlines[srcRowStart];

            // Copy raw index bytes
            byte[] curRow = new byte[width];
            int copyLen = Math.Min(width, filteredScanlines.Length - srcRowStart - 1);
            for (int x = 0; x < copyLen; x++)
                curRow[x] = filteredScanlines[srcRowStart + 1 + x];

            // Un-filter in index domain (bpp=1 for palette)
            ApplyPngUnFilter(filterType, curRow, prevRow, width, 1);

            // Expand palette indices to RGB, compositing alpha onto white if tRNS present
            int dstRowStart = row * width * 3;
            for (int x = 0; x < width; x++)
            {
                int idx = curRow[x];
                if (idx >= paletteCount)
                    idx = paletteCount - 1; // clamp out-of-range index

                byte r = plteData[idx * 3];
                byte g = plteData[idx * 3 + 1];
                byte b = plteData[idx * 3 + 2];

                if (trnsData != null && idx < trnsData.Length)
                {
                    // Alpha-over composite onto white: out = (alpha/255)*color + (1 - alpha/255)*255
                    float alpha = trnsData[idx] / 255f;
                    r = (byte)(alpha * r + (1f - alpha) * 255f + 0.5f);
                    g = (byte)(alpha * g + (1f - alpha) * 255f + 0.5f);
                    b = (byte)(alpha * b + (1f - alpha) * 255f + 0.5f);
                }

                rawPixels[dstRowStart + x * 3]     = r;
                rawPixels[dstRowStart + x * 3 + 1] = g;
                rawPixels[dstRowStart + x * 3 + 2] = b;
            }

            prevRow = curRow;
        }

        return rawPixels;
    }

    /// <summary>
    /// Decode 8-bit RGBA (color_type=6) PNG scanlines to raw RGB pixel buffer.
    /// Each pixel is 4 bytes (R, G, B, A). Alpha is composited onto a white background:
    /// out = (alpha/255)*color + (1 - alpha/255)*255.
    /// </summary>
    private static byte[] DecodePngRgba(byte[] filteredScanlines, int width, int height)
    {
        int bpp = 4; // bytes per pixel: R, G, B, A
        int bytesPerFilteredRow = width * bpp;
        int rowStride = bytesPerFilteredRow + 1; // +1 for filter byte
        byte[] rawPixels = new byte[height * width * 3];
        byte[] prevRow = new byte[bytesPerFilteredRow];

        for (int row = 0; row < height; row++)
        {
            int srcRowStart = row * rowStride;
            if (srcRowStart >= filteredScanlines.Length) break;

            byte filterType = filteredScanlines[srcRowStart];

            byte[] curRow = new byte[bytesPerFilteredRow];
            int copyLen = Math.Min(bytesPerFilteredRow, filteredScanlines.Length - srcRowStart - 1);
            for (int x = 0; x < copyLen; x++)
                curRow[x] = filteredScanlines[srcRowStart + 1 + x];

            // Un-filter in RGBA domain (bpp=4)
            ApplyPngUnFilter(filterType, curRow, prevRow, bytesPerFilteredRow, bpp);

            // Composite RGBA onto white
            int dstRowStart = row * width * 3;
            for (int x = 0; x < width; x++)
            {
                float alpha = curRow[x * bpp + 3] / 255f;
                rawPixels[dstRowStart + x * 3]     = (byte)(alpha * curRow[x * bpp]     + (1f - alpha) * 255f + 0.5f);
                rawPixels[dstRowStart + x * 3 + 1] = (byte)(alpha * curRow[x * bpp + 1] + (1f - alpha) * 255f + 0.5f);
                rawPixels[dstRowStart + x * 3 + 2] = (byte)(alpha * curRow[x * bpp + 2] + (1f - alpha) * 255f + 0.5f);
            }

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
            // background-color: fill a solid rectangle before any content
            if (el.Source?.BackgroundColor is { Length: > 0 } bgColorVal)
            {
                sb.AppendLine("ET");
                (float bgR, float bgG, float bgB) = ParseColor(bgColorVal);
                float bgX = el.Position.X;
                float bgY = pageHeightPt - el.Position.Y - el.Position.Height;
                float bgW = el.Position.Width;
                float bgH = el.Position.Height;
                sb.Append("q").AppendLine();
                sb.Append(bgR.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(bgG.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(bgB.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" rg");
                sb.Append(bgX.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(bgY.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(bgW.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(bgH.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" re");
                sb.AppendLine("f");
                sb.AppendLine("Q");
                sb.AppendLine("BT");
                currentFamily = null;
                currentSize = 0f;
            }

            // background-image: draw data-URI image as XObject at element bounds
            if (el.Source?.BackgroundImageSrc is { Length: > 0 } bgSrc &&
                srcToResName.TryGetValue(bgSrc, out string? bgImgRes))
            {
                sb.AppendLine("ET");
                float bgImgX = el.Position.X;
                float bgImgY = pageHeightPt - el.Position.Y - el.Position.Height;
                float bgImgW = el.Position.Width;
                float bgImgH = el.Position.Height;
                sb.AppendLine("q");
                sb.AppendLine($"{bgImgW.ToString("F4", CultureInfo.InvariantCulture)} 0 0 {bgImgH.ToString("F4", CultureInfo.InvariantCulture)} {bgImgX.ToString("F4", CultureInfo.InvariantCulture)} {bgImgY.ToString("F4", CultureInfo.InvariantCulture)} cm");
                sb.AppendLine($"/{bgImgRes} Do");
                sb.AppendLine("Q");
                sb.AppendLine("BT");
                currentFamily = null;
                currentSize = 0f;
            }

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

            // HrBox: draw a filled rectangle outside BT/ET
            if (el.Source is HrBox hr)
            {
                sb.AppendLine("ET");

                (float hr_r, float hr_g, float hr_b) = ParseHrColor(hr.Color);
                float hr_pdfY = pageHeightPt - el.Position.Y - hr.Thickness / 2f;
                sb.Append(hr_r.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(hr_g.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(hr_b.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" rg");
                sb.Append(el.Position.X.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(hr_pdfY.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(el.Position.Width.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(hr.Thickness.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" re");
                sb.AppendLine("f");

                sb.AppendLine("BT");
                currentFamily = null; // reset font state after re-opening BT
                currentSize = 0f;
                continue;
            }

            // TableCellBox: emit PDF stroke commands for visible border sides (G3 fix).
            // Phase 8.7 Wave 7 added background/border drawing for BlockBox PositionedElements
            // but never extended the path to TableCellBox. 10 of 17 real templates use
            // border-collapse:collapse with explicit 1px solid #008080 cell borders that were
            // silently dropped. This block draws each non-zero border side as a stroked line.
            if (el.Source is TableCellBox tcb)
            {
                bool drawTop    = tcb.BorderTop    > 0f;
                bool drawRight  = tcb.BorderRight  > 0f;
                bool drawBottom = tcb.BorderBottom > 0f;
                bool drawLeft   = tcb.BorderLeft   > 0f;
                if (drawTop || drawRight || drawBottom || drawLeft)
                {
                    sb.AppendLine("ET");

                    // Resolve border color from the source node's computed style.
                    // AngleSharp normalises border shorthand into individual border-*-color properties.
                    string? borderColorCss = tcb.Source?.Style?.GetValue("border-top-color")
                                          ?? tcb.Source?.Style?.GetValue("border-right-color")
                                          ?? tcb.Source?.Style?.GetValue("border-bottom-color")
                                          ?? tcb.Source?.Style?.GetValue("border-left-color")
                                          ?? tcb.Source?.Style?.GetValue("border-color")
                                          ?? "black";
                    (float cellR, float cellG, float cellB) = ParseColor(borderColorCss);

                    float cellX = el.Position.X;
                    float cellY = el.Position.Y;
                    float cellW = el.Position.Width;
                    float cellH = el.Position.Height;
                    // Y-flip: layout Y=0 at top → PDF Y=0 at bottom
                    float pdfBottom = pageHeightPt - cellY - cellH;
                    float pdfTop    = pageHeightPt - cellY;

                    // Line width: max of all four border sides (corpus = uniform 0.75pt ≈ 1 CSS px)
                    float lw = MathF.Max(MathF.Max(tcb.BorderTop, tcb.BorderBottom),
                                         MathF.Max(tcb.BorderLeft, tcb.BorderRight));
                    if (lw <= 0f) lw = 0.75f;

                    sb.AppendLine("q");
                    sb.Append(cellR.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                    sb.Append(cellG.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                    sb.Append(cellB.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" RG"); // stroke color
                    sb.Append(lw.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" w");

                    if (drawTop)
                    {
                        sb.Append(cellX.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfTop.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(" m ");
                        sb.Append((cellX + cellW).ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfTop.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" l S");
                    }
                    if (drawBottom)
                    {
                        sb.Append(cellX.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfBottom.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(" m ");
                        sb.Append((cellX + cellW).ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfBottom.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" l S");
                    }
                    if (drawLeft)
                    {
                        sb.Append(cellX.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfBottom.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(" m ");
                        sb.Append(cellX.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfTop.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" l S");
                    }
                    if (drawRight)
                    {
                        sb.Append((cellX + cellW).ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfBottom.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(" m ");
                        sb.Append((cellX + cellW).ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                        sb.Append(pdfTop.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" l S");
                    }

                    sb.AppendLine("Q");
                    sb.AppendLine("BT");
                    currentFamily = null;
                    currentSize = 0f;
                }
                continue; // TableCellBox: no inline text content at this node level
            }

            if (el.Source is not InlineBox inline || string.IsNullOrEmpty(inline.FontFamily))
                continue;

            // Use the per-word text stored by InlineLayoutEngine (Bug A fix).
            // InlineLayoutEngine word-splits each InlineBox and stores the individual word in
            // RenderedText. Falling back to inline.Text would draw the FULL source text (entire
            // line) at every word position, producing overlapping duplicate text.
            string renderText = el.RenderedText ?? inline.Text ?? string.Empty;
            if (string.IsNullOrEmpty(renderText))
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

            // Synthetic bold: set stroke color = fill color and stroke width scaled to font size.
            // Only applied when font size >= 8pt to avoid noisy artifacts at tiny sizes.
            bool syntheticBold = inline.Bold && inline.FontSize >= 8f;
            if (syntheticBold)
            {
                // Stroke color equals fill color (RG = stroke counterpart of rg)
                sb.Append(r.ToString("F4", CultureInfo.InvariantCulture));
                sb.Append(' ');
                sb.Append(g.ToString("F4", CultureInfo.InvariantCulture));
                sb.Append(' ');
                sb.Append(b.ToString("F4", CultureInfo.InvariantCulture));
                sb.AppendLine(" RG");

                // Stroke width scaled with font size; clamped to [0.2, 0.8]
                float strokeWidth = MathF.Max(0.2f, MathF.Min(0.8f, (inline.FontSize / 13f) * 0.4f));
                sb.Append(strokeWidth.ToString("F4", CultureInfo.InvariantCulture));
                sb.AppendLine(" w");
            }

            // Absolute positioning via Tm.
            // Synthetic italic: skew the text matrix with c=0.2 (≈11° slant).
            float pdfXt = el.Position.X;
            float pdfYt = pageHeightPt - el.Position.Y - inline.FontSize;
            if (inline.Italic)
            {
                sb.Append("1 0 0.2 1 ");
            }
            else
            {
                sb.Append("1 0 0 1 ");
            }
            sb.Append(pdfXt.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(' ');
            sb.Append(pdfYt.ToString("F4", CultureInfo.InvariantCulture));
            sb.AppendLine(" Tm");

            // Synthetic bold: switch to fill+stroke rendering mode (Tr=2) before Tj.
            if (syntheticBold)
                sb.AppendLine("2 Tr");

            // Text as 2-byte GID hex string using CID encoding.
            // Use renderText (the per-word segment), NOT inline.Text (the full source line).
            if (cpToNewGidMap.TryGetValue(inline.FontFamily, out Dictionary<int, ushort>? cpMap) && cpMap.Count > 0)
            {
                sb.Append('<');
                foreach (char c in renderText)
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
                // A missing or empty cpMap means the subsetter did not produce a cp→newGid mapping
                // for this font family. Under Identity-H encoding, emitting a Latin-1 literal would
                // be interpreted as 2-byte glyph IDs and produce nothing visible (silent blank output).
                // This violates the project's fail-loud rule — throw instead.
                throw new PdfFormatException(
                    "FONT-GID-MAP-MISSING",
                    $"Font GID map missing or empty for family '{inline.FontFamily}'. " +
                    "Ensure the font is declared in @font-face, the font file is resolvable, " +
                    "and the subsetter produced a valid cp→newGid mapping.");
            }

            // Synthetic bold: restore fill-only rendering mode immediately after Tj.
            // Critical — without this reset, subsequent non-bold text would render bold.
            if (syntheticBold)
                sb.AppendLine("0 Tr");

            // text-decoration: draw underline or strikethrough outside BT/ET
            if (inline.TextDecoration is "underline" or "line-through")
            {
                float decThickness = inline.FontSize * 0.07f;
                float baselineY = pdfYt; // already in PDF coords (Y=0 at bottom)
                float decY = inline.TextDecoration == "underline"
                    ? baselineY - inline.FontSize * 0.1f
                    : baselineY + inline.FontSize * 0.35f;
                float decX = el.Position.X;
                float decW = el.Position.Width;

                // Capture current font/size for restatement
                string savedFamily = currentFamily ?? "";
                float savedSize = currentSize;

                sb.AppendLine("ET");
                sb.Append("q").AppendLine();
                sb.Append(r.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(g.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(b.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" rg");
                sb.Append(decX.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(decY.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(decW.ToString("F4", CultureInfo.InvariantCulture)); sb.Append(' ');
                sb.Append(decThickness.ToString("F4", CultureInfo.InvariantCulture)); sb.AppendLine(" re");
                sb.AppendLine("f");
                sb.AppendLine("Q");
                sb.AppendLine("BT");

                // Restate font/size after closing and reopening BT
                if (!string.IsNullOrEmpty(savedFamily))
                {
                    string resNameAfterDec = familyToResName.TryGetValue(savedFamily, out string? rnAfterDec) ? rnAfterDec : "F0";
                    sb.Append('/');
                    sb.Append(resNameAfterDec);
                    sb.Append(' ');
                    sb.Append(savedSize.ToString("F2", CultureInfo.InvariantCulture));
                    sb.AppendLine(" Tf");
                }
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

    /// <summary>Escapes a URI string for use inside a PDF literal string ( ... ).</summary>
    private static string EscapePdfString(string value)
    {
        var sb = new StringBuilder(value.Length + 4);
        foreach (char c in value)
        {
            switch (c)
            {
                case '(': sb.Append("\\("); break;
                case ')': sb.Append("\\)"); break;
                case '\\': sb.Append("\\\\"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
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
            "teal" => (0f, 0.502f, 0.502f),
            "navy" => (0f, 0f, 0.502f),
            "maroon" => (0.502f, 0f, 0f),
            "purple" => (0.502f, 0f, 0.502f),
            "olive" => (0.502f, 0.502f, 0f),
            "silver" => (0.753f, 0.753f, 0.753f),
            "lime" => (0f, 1f, 0f),
            "aqua" or "cyan" => (0f, 1f, 1f),
            "fuchsia" or "magenta" => (1f, 0f, 1f),
            "orange" => (1f, 0.647f, 0f),
            _ when c.Length == 7 && c[0] == '#' => ParseHexColor(c),
            _ when c.Length == 4 && c[0] == '#' => ParseHexColorShort(c),
            _ when c.StartsWith("rgb(", StringComparison.Ordinal) => ParseRgbColor(c),
            _ when c.StartsWith("rgba(", StringComparison.Ordinal) => ParseRgbaColor(c),
            _ => (0f, 0f, 0f)
        };
    }

    /// <summary>
    /// Parses CSS 3-char shorthand hex color (#rgb → #rrggbb).
    /// </summary>
    private static (float R, float G, float B) ParseHexColorShort(string c)
    {
        // #abc → #aabbcc
        if (int.TryParse(c.AsSpan(1, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int r) &&
            int.TryParse(c.AsSpan(2, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int g) &&
            int.TryParse(c.AsSpan(3, 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int b))
            return ((r * 17) / 255f, (g * 17) / 255f, (b * 17) / 255f);
        return (0f, 0f, 0f);
    }

    /// <summary>
    /// Parses CSS rgb(R, G, B) where components are 0–255 integers.
    /// AngleSharp normalises all color values (including hex) to rgb/rgba format.
    /// </summary>
    private static (float R, float G, float B) ParseRgbColor(string c)
    {
        // c = "rgb(r, g, b)" (lower-cased, trimmed)
        int open = c.IndexOf('(');
        int close = c.IndexOf(')');
        if (open < 0 || close < 0 || close <= open) return (0f, 0f, 0f);
        ReadOnlySpan<char> inner = c.AsSpan(open + 1, close - open - 1);
        Span<Range> parts = stackalloc Range[4];
        int count = inner.Split(parts, ',', StringSplitOptions.TrimEntries);
        if (count < 3) return (0f, 0f, 0f);
        if (TryParseColorComponent(inner[parts[0]], out float r) &&
            TryParseColorComponent(inner[parts[1]], out float g) &&
            TryParseColorComponent(inner[parts[2]], out float b))
            return (r, g, b);
        return (0f, 0f, 0f);
    }

    /// <summary>
    /// Parses CSS rgba(R, G, B, A) — ignores alpha (PDF uses opaque fills).
    /// </summary>
    private static (float R, float G, float B) ParseRgbaColor(string c)
    {
        // rgba(...) shares the same component layout; reuse rgb parser (alpha ignored in PDF)
        int open = c.IndexOf('(');
        int close = c.IndexOf(')');
        if (open < 0 || close < 0 || close <= open) return (0f, 0f, 0f);
        ReadOnlySpan<char> inner = c.AsSpan(open + 1, close - open - 1);
        Span<Range> parts = stackalloc Range[5];
        int count = inner.Split(parts, ',', StringSplitOptions.TrimEntries);
        if (count < 3) return (0f, 0f, 0f);
        if (TryParseColorComponent(inner[parts[0]], out float r) &&
            TryParseColorComponent(inner[parts[1]], out float g) &&
            TryParseColorComponent(inner[parts[2]], out float b))
            return (r, g, b);
        return (0f, 0f, 0f);
    }

    /// <summary>
    /// Parses a single CSS color component: integer 0–255 → 0.0–1.0f.
    /// </summary>
    private static bool TryParseColorComponent(ReadOnlySpan<char> span, out float value)
    {
        if (float.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out float raw))
        {
            value = raw / 255f;
            return true;
        }
        value = 0f;
        return false;
    }

    /// <summary>
    /// Parses an HR color: accepts "r g b" float triplet (space-separated) or CSS color keyword/hex.
    /// Null → default gray 0.5 0.5 0.5.
    /// </summary>
    private static (float R, float G, float B) ParseHrColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return (0.5f, 0.5f, 0.5f);

        // Try "r g b" triplet format first
        string[] parts = color.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3 &&
            float.TryParse(parts[0], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float r) &&
            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float g) &&
            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out float b))
        {
            return (r, g, b);
        }

        // Fall back to CSS color parsing
        return ParseColor(color);
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
