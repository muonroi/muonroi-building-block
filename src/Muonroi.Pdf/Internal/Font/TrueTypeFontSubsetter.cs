namespace Muonroi.Pdf.Internal.Font;

/// <summary>
/// Result of a TrueType font subset operation. Contains the subset bytes plus the GID mapping
/// computed during subsetting, which Plan 02 (CID font embedding) needs to emit /W arrays and
/// 2-byte GID content streams without a redundant cmap parse.
/// </summary>
/// <param name="SubsetBytes">The subset TTF bytes.</param>
/// <param name="OldToNewGid">Maps original GID → renumbered GID in the subset font.</param>
/// <param name="SortedGids">Renumbered GIDs in ascending order (for /W array generation).</param>
/// <param name="CpToNewGid">
/// Authoritative codepoint → new GID mapping built at subsetting time.
/// The writer uses this directly to emit 2-byte GID hex strings; no post-hoc cmap parse needed.
/// </param>
internal sealed record FontSubsetResult(
    ReadOnlyMemory<byte> SubsetBytes,
    IReadOnlyDictionary<ushort, ushort> OldToNewGid,
    IReadOnlyList<ushort> SortedGids,
    IReadOnlyDictionary<int, ushort> CpToNewGid);

[SuppressMessage("Muonroi.CodeStandards", "MSTD0001",
    Justification = "PdfFormatException is the public PDF-contract exception type; consumers catch it directly. Cannot change hierarchy.")]
internal sealed class TrueTypeFontSubsetter
{
    private const uint SfntVersionTTF  = 0x00010000u;
    private const uint SfntVersionCFF  = 0x4F54544Fu; // 'OTTO'
    private const uint SfntVersionWOFF  = 0x774F4646u; // 'wOFF'
    private const uint SfntVersionWOFF2 = 0x774F4632u; // 'wOF2'

    internal FontSubsetResult Subset(ReadOnlyMemory<byte> fontBytes, IReadOnlySet<int> usedCodepoints)
    {
        if (fontBytes.Length < 12)
            throw new PdfFormatException("FONT-FORMAT", "Font too short to parse sfntVersion");

        uint sfntVersion = BinaryPrimitives.ReadUInt32BigEndian(fontBytes.Span);

        // OTF-CFF (PostScript outlines): never pass through — would produce a corrupted GID map
        if (sfntVersion == SfntVersionCFF)
            throw new PdfFormatException("FONT-OTF-CFF",
                "OTF font with CFF/PostScript outlines (sfntVersion=0x4F54544F) is not supported. " +
                "Convert the font to TrueType outlines (.ttf with sfntVersion=0x00010000) before embedding, " +
                "or use a TTF variant of the same typeface.");

        // WOFF/WOFF2 web fonts: must be converted to TTF before embedding
        if (sfntVersion == SfntVersionWOFF || sfntVersion == SfntVersionWOFF2)
            throw new PdfFormatException("FONT-WOFF",
                "WOFF/WOFF2 web fonts are not supported. Convert to a TrueType (.ttf) font before embedding.");

        if (sfntVersion != SfntVersionTTF)
            throw new PdfFormatException("FONT-FORMAT",
                $"Unrecognized font format (sfntVersion: 0x{sfntVersion:X8})");

        // --- Step 2: Parse Table Directory ---
        ushort numTables = BinaryPrimitives.ReadUInt16BigEndian(fontBytes.Span.Slice(4, 2));
        if (numTables < 1 || numTables > 100)
            throw new PdfFormatException("FONT-FORMAT", $"numTables out of range: {numTables}");

        var tables = new Dictionary<string, (uint offset, uint length)>(StringComparer.Ordinal);
        for (int i = 0; i < numTables; i++)
        {
            int rec = 12 + i * 16;
            if (rec + 16 > fontBytes.Length)
                throw new PdfFormatException("FONT-FORMAT", "Table directory extends beyond font bounds");

            string tag = Encoding.ASCII.GetString(fontBytes.Span.Slice(rec, 4));
            uint tOffset = BinaryPrimitives.ReadUInt32BigEndian(fontBytes.Span.Slice(rec + 8, 4));
            uint tLength = BinaryPrimitives.ReadUInt32BigEndian(fontBytes.Span.Slice(rec + 12, 4));

            if (tOffset + tLength > (uint)fontBytes.Length)
                throw new PdfFormatException("FONT-FORMAT", $"Table '{tag}' extends beyond font bounds");

            tables[tag] = (tOffset, tLength);
        }

        // --- Step 3: cmap → GIDs ---
        HashSet<ushort> usedGids = BuildUsedGids(fontBytes.Span, tables, usedCodepoints);

        // --- Step 4: composite glyph closure ---
        if (!tables.TryGetValue("loca", out var locaTable) || !tables.TryGetValue("glyf", out _))
            return new FontSubsetResult(fontBytes, new Dictionary<ushort, ushort>(), Array.Empty<ushort>(), new Dictionary<int, ushort>()); // malformed; pass through

        int indexToLocFormat = ReadIndexToLocFormat(fontBytes.Span, tables);
        ExpandCompositeGlyphs(fontBytes.Span, tables, usedGids, indexToLocFormat);

        // --- Step 5: Build subset font ---
        return BuildSubsetFont(fontBytes.Span, tables, usedGids, usedCodepoints, indexToLocFormat);
    }

    // ── cmap parsing ─────────────────────────────────────────────────────────

    private static HashSet<ushort> BuildUsedGids(
        ReadOnlySpan<byte> font,
        Dictionary<string, (uint offset, uint length)> tables,
        IReadOnlySet<int> usedCodepoints)
    {
        var gids = new HashSet<ushort> { 0 }; // always include .notdef

        if (!tables.TryGetValue("cmap", out var cmap))
            return gids;

        int cmapBase = (int)cmap.offset;
        if (cmapBase + 4 > font.Length) return gids;

        ushort numSubtables = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(cmapBase + 2, 2));

        // Find Format 4 subtable (platform 3 encoding 1 preferred, else any)
        int fmt4Offset = -1;
        for (int i = 0; i < numSubtables; i++)
        {
            int er = cmapBase + 4 + i * 8;
            if (er + 8 > font.Length) break;
            ushort fmt = BinaryPrimitives.ReadUInt16BigEndian(
                font.Slice(cmapBase + (int)BinaryPrimitives.ReadUInt32BigEndian(font.Slice(er + 4, 4)), 2));
            if (fmt == 4)
            {
                fmt4Offset = cmapBase + (int)BinaryPrimitives.ReadUInt32BigEndian(font.Slice(er + 4, 4));
                ushort platformId = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(er, 2));
                ushort encodingId = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(er + 2, 2));
                if (platformId == 3 && encodingId == 1)
                    break; // prefer Windows BMP
            }
        }

        if (fmt4Offset < 0) return gids;

        // Parse Format 4
        int segCountX2 = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(fmt4Offset + 6, 2));
        int segCount = segCountX2 / 2;
        int endCodesOff = fmt4Offset + 14;
        int startCodesOff = endCodesOff + segCountX2 + 2;
        int deltaOff = startCodesOff + segCountX2;
        int rangeOffOff = deltaOff + segCountX2;
        int glyphIdArrayOff = rangeOffOff + segCountX2;

        foreach (int cp in usedCodepoints)
        {
            if (cp < 0 || cp > 0xFFFF) continue;
            ushort c = (ushort)cp;

            for (int s = 0; s < segCount; s++)
            {
                ushort endCode = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(endCodesOff + s * 2, 2));
                if (c > endCode) continue;

                ushort startCode = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(startCodesOff + s * 2, 2));
                if (c < startCode) break;

                short delta = (short)BinaryPrimitives.ReadUInt16BigEndian(font.Slice(deltaOff + s * 2, 2));
                ushort rangeOff = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(rangeOffOff + s * 2, 2));

                ushort gid;
                if (rangeOff == 0)
                {
                    gid = (ushort)((c + delta) & 0xFFFF);
                }
                else
                {
                    int glyphIdIdx = rangeOffOff + s * 2 + rangeOff + (c - startCode) * 2;
                    if (glyphIdIdx + 2 > font.Length) break;
                    ushort rawGid = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(glyphIdIdx, 2));
                    gid = rawGid == 0 ? (ushort)0 : (ushort)((rawGid + delta) & 0xFFFF);
                }

                if (gid != 0)
                    gids.Add(gid);
                break;
            }
        }

        return gids;
    }

    // ── composite glyph closure ───────────────────────────────────────────────

    private static void ExpandCompositeGlyphs(
        ReadOnlySpan<byte> font,
        Dictionary<string, (uint offset, uint length)> tables,
        HashSet<ushort> usedGids,
        int indexToLocFormat)
    {
        var pending = new Queue<ushort>(usedGids);
        while (pending.Count > 0)
        {
            ushort gid = pending.Dequeue();
            foreach (ushort comp in GetComponentGids(font, tables, gid, indexToLocFormat))
            {
                if (usedGids.Add(comp))
                    pending.Enqueue(comp);
            }
        }
    }

    private static bool IsComposite(ReadOnlySpan<byte> font, (uint offset, uint length) glyf,
        (uint offset, uint length) loca, ushort gid, int indexToLocFormat)
    {
        int glyfStart = GetGlyfOffset(font, loca, gid, indexToLocFormat);
        if (glyfStart < 0) return false;
        if ((int)glyf.offset + glyfStart + 2 > font.Length) return false;
        short numContours = (short)BinaryPrimitives.ReadUInt16BigEndian(
            font.Slice((int)glyf.offset + glyfStart, 2));
        return numContours < 0;
    }

    private static List<ushort> GetComponentGids(
        ReadOnlySpan<byte> font,
        Dictionary<string, (uint offset, uint length)> tables,
        ushort gid,
        int indexToLocFormat)
    {
        var result = new List<ushort>();
        if (!tables.TryGetValue("glyf", out var glyf) || !tables.TryGetValue("loca", out var loca))
            return result;

        if (!IsComposite(font, glyf, loca, gid, indexToLocFormat))
            return result;

        int glyfStart = GetGlyfOffset(font, loca, gid, indexToLocFormat);
        if (glyfStart < 0) return result;

        int pos = (int)glyf.offset + glyfStart + 10; // skip numberOfContours(2)+bbox(8)
        const ushort MORE_COMPONENTS = 0x0020;
        const ushort ARG_1_AND_2_ARE_WORDS = 0x0001;
        const ushort WE_HAVE_A_SCALE = 0x0008;
        const ushort WE_HAVE_AN_X_AND_Y_SCALE = 0x0040;
        const ushort WE_HAVE_A_TWO_BY_TWO = 0x0080;

        while (pos + 4 <= font.Length)
        {
            ushort flags = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(pos, 2));
            ushort compGid = BinaryPrimitives.ReadUInt16BigEndian(font.Slice(pos + 2, 2));
            result.Add(compGid);

            pos += 4;
            pos += (flags & ARG_1_AND_2_ARE_WORDS) != 0 ? 4 : 2;
            if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0) pos += 8;
            else if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0) pos += 4;
            else if ((flags & WE_HAVE_A_SCALE) != 0) pos += 2;

            if ((flags & MORE_COMPONENTS) == 0) break;
        }

        return result;
    }

    private static int GetGlyfOffset(ReadOnlySpan<byte> font, (uint offset, uint length) loca,
        ushort gid, int indexToLocFormat)
    {
        if (indexToLocFormat == 1)
        {
            int idx = (int)loca.offset + gid * 4;
            if (idx + 8 > font.Length) return -1;
            uint o0 = BinaryPrimitives.ReadUInt32BigEndian(font.Slice(idx, 4));
            uint o1 = BinaryPrimitives.ReadUInt32BigEndian(font.Slice(idx + 4, 4));
            return o0 == o1 ? -1 : (int)o0; // empty glyph if equal
        }
        else
        {
            int idx = (int)loca.offset + gid * 2;
            if (idx + 4 > font.Length) return -1;
            uint o0 = (uint)BinaryPrimitives.ReadUInt16BigEndian(font.Slice(idx, 2)) * 2;
            uint o1 = (uint)BinaryPrimitives.ReadUInt16BigEndian(font.Slice(idx + 2, 2)) * 2;
            return o0 == o1 ? -1 : (int)o0;
        }
    }

    private static int ReadIndexToLocFormat(ReadOnlySpan<byte> font,
        Dictionary<string, (uint offset, uint length)> tables)
    {
        if (!tables.TryGetValue("head", out var head)) return 0;
        int off = (int)head.offset + 50;
        if (off + 2 > font.Length) return 0;
        return BinaryPrimitives.ReadInt16BigEndian(font.Slice(off, 2));
    }

    // ── subset font builder ───────────────────────────────────────────────────

    private static FontSubsetResult BuildSubsetFont(
        ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> srcTables,
        HashSet<ushort> usedGids,
        IReadOnlySet<int> usedCodepoints,
        int srcLocFormat)
    {
        ushort[] sortedGids = [.. usedGids.OrderBy(g => g)];
        int newGlyphCount = sortedGids.Length;
        var oldToNew = new Dictionary<ushort, ushort>(newGlyphCount);
        for (ushort ni = 0; ni < sortedGids.Length; ni++)
            oldToNew[sortedGids[ni]] = ni;

        // Build rebuilt table data
        byte[] glyfData = BuildGlyfTable(src, srcTables, sortedGids, oldToNew, srcLocFormat);
        byte[] locaData = BuildLocaTable(src, srcTables, sortedGids, srcLocFormat); // long format
        byte[] hmtxData = BuildHmtxTable(src, srcTables, sortedGids);
        (byte[] cmapData, IReadOnlyDictionary<int, ushort> cpToNewGid) = BuildCmapTable(src, srcTables, usedCodepoints, oldToNew);
        byte[] maxpData = PatchMaxp(src, srcTables, (ushort)newGlyphCount);
        byte[] headData = PatchHead(src, srcTables); // checksumAdjustment set at end

        // Decide which tables to include
        var outTables = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["cmap"] = cmapData,
            ["glyf"] = glyfData,
            ["head"] = headData,
            ["hhea"] = CopyTable(src, srcTables, "hhea"),
            ["hmtx"] = hmtxData,
            ["loca"] = locaData,
            ["maxp"] = maxpData,
        };

        if (srcTables.ContainsKey("OS/2")) outTables["OS/2"] = CopyTable(src, srcTables, "OS/2");
        if (srcTables.ContainsKey("name")) outTables["name"] = CopyTable(src, srcTables, "name");
        if (srcTables.ContainsKey("post")) outTables["post"] = CopyTable(src, srcTables, "post");
        if (srcTables.ContainsKey("kern")) outTables["kern"] = CopyTable(src, srcTables, "kern");
        if (srcTables.ContainsKey("cvt ")) outTables["cvt "] = CopyTable(src, srcTables, "cvt ");
        if (srcTables.ContainsKey("fpgm")) outTables["fpgm"] = CopyTable(src, srcTables, "fpgm");
        if (srcTables.ContainsKey("prep")) outTables["prep"] = CopyTable(src, srcTables, "prep");

        string[] sortedTags = [.. outTables.Keys.OrderBy(t => t, StringComparer.Ordinal)];
        int numOut = sortedTags.Length;

        // Calculate layout
        int headerSize = 12 + numOut * 16;
        // Pad each table to 4-byte boundary
        int[] tableAlignedSizes = new int[numOut];
        int[] tableOffsets = new int[numOut];
        int dataPos = headerSize;
        for (int i = 0; i < numOut; i++)
        {
            tableOffsets[i] = dataPos;
            int len = outTables[sortedTags[i]].Length;
            tableAlignedSizes[i] = (len + 3) & ~3;
            dataPos += tableAlignedSizes[i];
        }

        byte[] output = new byte[dataPos];

        // Write sfnt header
        BinaryPrimitives.WriteUInt32BigEndian(output, SfntVersionTTF);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(4), (ushort)numOut);
        int sr = MaxPow2Le(numOut) * 16;
        int es = Log2(MaxPow2Le(numOut));
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(6), (ushort)sr);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(8), (ushort)es);
        BinaryPrimitives.WriteUInt16BigEndian(output.AsSpan(10), (ushort)(numOut * 16 - sr));

        // Write table records and data
        int headTableIdx = -1;
        for (int i = 0; i < numOut; i++)
        {
            string tag = sortedTags[i];
            byte[] data = outTables[tag];
            int recOff = 12 + i * 16;

            // Tag
            byte[] tagBytes = Encoding.ASCII.GetBytes(tag.PadRight(4)[..4]);
            tagBytes.CopyTo(output, recOff);

            // Checksum
            uint ck = CalcChecksum(data);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recOff + 4), ck);

            // Offset and length
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recOff + 8), (uint)tableOffsets[i]);
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(recOff + 12), (uint)data.Length);

            // Copy data
            data.CopyTo(output, tableOffsets[i]);

            if (tag == "head") headTableIdx = i;
        }

        // Whole-file checksum and write checksumAdjustment
        uint fileChecksum = CalcChecksum(output);
        uint adjustment = 0xB1B0AFBAu - fileChecksum;
        if (headTableIdx >= 0)
        {
            int headDataOff = tableOffsets[headTableIdx] + 8; // checksumAdjustment at offset 8 in head
            BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(headDataOff), adjustment);
        }

        return new FontSubsetResult(
            new ReadOnlyMemory<byte>(output),
            oldToNew,
            sortedGids,
            cpToNewGid);
    }

    // ── table builders ────────────────────────────────────────────────────────

    private static byte[] BuildGlyfTable(ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables,
        ushort[] sortedGids,
        Dictionary<ushort, ushort> oldToNew,
        int srcLocFormat)
    {
        if (!tables.TryGetValue("glyf", out var glyf) || !tables.TryGetValue("loca", out var loca))
            return [];

        var ms = new MemoryStream();
        foreach (ushort gid in sortedGids)
        {
            int glyfOff = GetGlyfOffset(src, loca, gid, srcLocFormat);
            if (glyfOff < 0)
            {
                // empty glyph — write nothing (loca consecutive entries will be equal)
                continue;
            }

            int absOff = (int)glyf.offset + glyfOff;
            if (absOff + 2 > src.Length) continue;

            short numContours = (short)BinaryPrimitives.ReadUInt16BigEndian(src.Slice(absOff, 2));

            int glyfLen;
            // Determine glyph length from next loca entry
            if (srcLocFormat == 1)
            {
                int locIdx = (int)loca.offset + gid * 4;
                if (locIdx + 8 > src.Length) continue;
                uint o0 = BinaryPrimitives.ReadUInt32BigEndian(src.Slice(locIdx, 4));
                uint o1 = BinaryPrimitives.ReadUInt32BigEndian(src.Slice(locIdx + 4, 4));
                glyfLen = (int)(o1 - o0);
            }
            else
            {
                int locIdx = (int)loca.offset + gid * 2;
                if (locIdx + 4 > src.Length) continue;
                uint o0 = (uint)BinaryPrimitives.ReadUInt16BigEndian(src.Slice(locIdx, 2)) * 2;
                uint o1 = (uint)BinaryPrimitives.ReadUInt16BigEndian(src.Slice(locIdx + 2, 2)) * 2;
                glyfLen = (int)(o1 - o0);
            }

            if (glyfLen <= 0 || absOff + glyfLen > src.Length) continue;

            byte[] glyphBytes = src.Slice(absOff, glyfLen).ToArray();

            if (numContours < 0) // composite — remap component GIDs
                PatchCompositeGlyph(glyphBytes, oldToNew);

            // Pad to 4-byte boundary
            ms.Write(glyphBytes);
            int pad = (4 - (glyphBytes.Length & 3)) & 3;
            for (int p = 0; p < pad; p++) ms.WriteByte(0);
        }

        return ms.ToArray();
    }

    private static void PatchCompositeGlyph(byte[] data, Dictionary<ushort, ushort> oldToNew)
    {
        const ushort MORE_COMPONENTS = 0x0020;
        const ushort ARG_1_AND_2_ARE_WORDS = 0x0001;
        const ushort WE_HAVE_A_SCALE = 0x0008;
        const ushort WE_HAVE_AN_X_AND_Y_SCALE = 0x0040;
        const ushort WE_HAVE_A_TWO_BY_TWO = 0x0080;

        int pos = 10; // skip numberOfContours(2) + bbox(8)
        while (pos + 4 <= data.Length)
        {
            ushort flags = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos, 2));
            ushort oldGid = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(pos + 2, 2));
            if (oldToNew.TryGetValue(oldGid, out ushort newGid))
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(pos + 2, 2), newGid);

            pos += 4;
            pos += (flags & ARG_1_AND_2_ARE_WORDS) != 0 ? 4 : 2;
            if ((flags & WE_HAVE_A_TWO_BY_TWO) != 0) pos += 8;
            else if ((flags & WE_HAVE_AN_X_AND_Y_SCALE) != 0) pos += 4;
            else if ((flags & WE_HAVE_A_SCALE) != 0) pos += 2;

            if ((flags & MORE_COMPONENTS) == 0) break;
        }
    }

    private static byte[] BuildLocaTable(ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables,
        ushort[] sortedGids,
        int srcLocFormat)
    {
        if (!tables.TryGetValue("glyf", out var glyf) || !tables.TryGetValue("loca", out var loca))
            return [];

        // Rebuild long-format loca (indexToLocFormat = 1)
        int glyphCount = sortedGids.Length;
        byte[] result = new byte[(glyphCount + 1) * 4];
        uint curOff = 0;

        for (int ni = 0; ni < glyphCount; ni++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(ni * 4), curOff);

            ushort gid = sortedGids[ni];
            int glyfOff = GetGlyfOffset(src, loca, gid, srcLocFormat);
            if (glyfOff < 0) continue; // empty glyph — next entry will equal this one

            int absOff = (int)glyf.offset + glyfOff;
            int glyfLen;
            if (srcLocFormat == 1)
            {
                int locIdx = (int)loca.offset + gid * 4;
                if (locIdx + 8 > src.Length) continue;
                uint o0 = BinaryPrimitives.ReadUInt32BigEndian(src.Slice(locIdx, 4));
                uint o1 = BinaryPrimitives.ReadUInt32BigEndian(src.Slice(locIdx + 4, 4));
                glyfLen = (int)(o1 - o0);
            }
            else
            {
                int locIdx = (int)loca.offset + gid * 2;
                if (locIdx + 4 > src.Length) continue;
                uint o0 = (uint)BinaryPrimitives.ReadUInt16BigEndian(src.Slice(locIdx, 2)) * 2;
                uint o1 = (uint)BinaryPrimitives.ReadUInt16BigEndian(src.Slice(locIdx + 2, 2)) * 2;
                glyfLen = (int)(o1 - o0);
            }

            if (glyfLen <= 0 || absOff + glyfLen > src.Length) continue;

            int paddedLen = (glyfLen + 3) & ~3;
            curOff += (uint)paddedLen;
        }

        BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(glyphCount * 4), curOff);
        return result;
    }

    private static byte[] BuildHmtxTable(ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables,
        ushort[] sortedGids)
    {
        if (!tables.TryGetValue("hmtx", out var hmtx) || !tables.TryGetValue("hhea", out var hhea))
            return [];

        int numberOfHMetrics = BinaryPrimitives.ReadUInt16BigEndian(src.Slice((int)hhea.offset + 34, 2));
        byte[] result = new byte[sortedGids.Length * 4];

        for (int ni = 0; ni < sortedGids.Length; ni++)
        {
            ushort gid = sortedGids[ni];
            int srcIdx = gid < numberOfHMetrics ? gid : numberOfHMetrics - 1;
            int srcOff = (int)hmtx.offset + srcIdx * 4;
            if (srcOff + 4 <= src.Length)
                src.Slice(srcOff, 4).CopyTo(result.AsSpan(ni * 4));
        }

        return result;
    }

    private static (byte[] CmapBytes, IReadOnlyDictionary<int, ushort> CpToNewGid) BuildCmapTable(
        ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables,
        IReadOnlySet<int> usedCodepoints,
        Dictionary<ushort, ushort> oldToNew)
    {
        // Build minimal Format 4 cmap with remapped GIDs.
        // Also return the authoritative cp→newGid map so callers can emit correct GID hex streams
        // without a post-hoc cmap parse.
        var pairs = new List<(ushort cp, ushort gid)>();
        if (tables.TryGetValue("cmap", out var cmap))
        {
            int cmapBase = (int)cmap.offset;
            if (cmapBase + 4 <= src.Length)
            {
                ushort numSubtables = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(cmapBase + 2, 2));
                int fmt4Off = -1;
                for (int i = 0; i < numSubtables; i++)
                {
                    int er = cmapBase + 4 + i * 8;
                    if (er + 8 > src.Length) break;
                    uint subOff = BinaryPrimitives.ReadUInt32BigEndian(src.Slice(er + 4, 4));
                    int subAbs = cmapBase + (int)subOff;
                    if (subAbs + 2 > src.Length) continue;
                    ushort fmt = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(subAbs, 2));
                    if (fmt == 4)
                    {
                        fmt4Off = subAbs;
                        ushort pid = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(er, 2));
                        ushort eid = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(er + 2, 2));
                        if (pid == 3 && eid == 1) break;
                    }
                }

                if (fmt4Off >= 0)
                {
                    int srcSegCountX2 = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(fmt4Off + 6, 2));
                    int srcSegCount = srcSegCountX2 / 2;
                    int endCodesOff = fmt4Off + 14;
                    int startCodesOff = endCodesOff + srcSegCountX2 + 2;
                    int deltaOff = startCodesOff + srcSegCountX2;
                    int rangeOffOff = deltaOff + srcSegCountX2;

                    foreach (int cp in usedCodepoints)
                    {
                        if (cp < 0 || cp > 0xFFFF) continue;
                        ushort c = (ushort)cp;
                        for (int s = 0; s < srcSegCount; s++)
                        {
                            ushort endCode = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(endCodesOff + s * 2, 2));
                            if (c > endCode) continue;
                            ushort startCode = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(startCodesOff + s * 2, 2));
                            if (c < startCode) break;
                            short delta = (short)BinaryPrimitives.ReadUInt16BigEndian(src.Slice(deltaOff + s * 2, 2));
                            ushort rangeOff = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(rangeOffOff + s * 2, 2));
                            ushort oldGid;
                            if (rangeOff == 0)
                                oldGid = (ushort)((c + delta) & 0xFFFF);
                            else
                            {
                                int glyphIdIdx = rangeOffOff + s * 2 + rangeOff + (c - startCode) * 2;
                                if (glyphIdIdx + 2 > src.Length) break;
                                ushort rawGid = BinaryPrimitives.ReadUInt16BigEndian(src.Slice(glyphIdIdx, 2));
                                oldGid = rawGid == 0 ? (ushort)0 : (ushort)((rawGid + delta) & 0xFFFF);
                            }
                            if (oldGid != 0 && oldToNew.TryGetValue(oldGid, out ushort newGid))
                                pairs.Add((c, newGid));
                            break;
                        }
                    }
                }
            }
        }

        pairs.Sort((a, b) => a.cp.CompareTo(b.cp));

        // Build segments: each cp gets its own 1-cp segment for simplicity
        // Add terminator segment 0xFFFF → 0xFFFF
        var segments = pairs.Select(p => (start: p.cp, end: p.cp, delta: (short)(p.gid - p.cp), rangeOff: (ushort)0)).ToList();
        segments.Add((0xFFFF, 0xFFFF, 1, 0)); // terminator

        int segCount = segments.Count;
        int fmt4Len = 14 + segCount * 8 + 2; // header + 4 arrays + reservedPad

        var ms = new MemoryStream();
        void WriteU16(ushort v) { ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)(v & 0xFF)); }
        void WriteU32(uint v) { ms.WriteByte((byte)(v >> 24)); ms.WriteByte((byte)(v >> 16)); ms.WriteByte((byte)(v >> 8)); ms.WriteByte((byte)(v & 0xFF)); }

        // cmap header: version=0, numTables=1
        WriteU16(0); WriteU16(1);
        // encoding record: platform=3, encoding=1, offset=12 (right after this record)
        WriteU16(3); WriteU16(1); WriteU32(12);

        // Format 4 subtable
        WriteU16(4); // format
        WriteU16((ushort)fmt4Len); // length
        WriteU16(0); // language
        WriteU16((ushort)(segCount * 2)); // segCountX2
        int sp2 = MaxPow2Le(segCount) * 2;
        WriteU16((ushort)sp2); // searchRange
        WriteU16((ushort)Log2(MaxPow2Le(segCount))); // entrySelector
        WriteU16((ushort)(segCount * 2 - sp2)); // rangeShift

        foreach (var seg in segments) WriteU16(seg.end);
        WriteU16(0); // reservedPad
        foreach (var seg in segments) WriteU16(seg.start);
        foreach (var seg in segments) WriteU16((ushort)seg.delta);
        foreach (var seg in segments) WriteU16(seg.rangeOff);

        // Build the authoritative cp→newGid dictionary from the sorted pairs
        var cpToNewGid = new Dictionary<int, ushort>(pairs.Count);
        foreach ((ushort cp, ushort gid) in pairs)
            cpToNewGid[cp] = gid;

        return (ms.ToArray(), cpToNewGid);
    }

    private static byte[] PatchMaxp(ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables,
        ushort newGlyphCount)
    {
        if (!tables.TryGetValue("maxp", out var maxp)) return [];
        byte[] data = src.Slice((int)maxp.offset, (int)maxp.length).ToArray();
        if (data.Length >= 6)
            BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4, 2), newGlyphCount);
        return data;
    }

    private static byte[] PatchHead(ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables)
    {
        if (!tables.TryGetValue("head", out var head)) return [];
        byte[] data = src.Slice((int)head.offset, (int)head.length).ToArray();
        // indexToLocFormat = 1 (long format), at offset 50
        if (data.Length >= 52)
            BinaryPrimitives.WriteInt16BigEndian(data.AsSpan(50, 2), 1);
        // checksumAdjustment = 0 initially (offset 8)
        if (data.Length >= 12)
            BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8, 4), 0);
        return data;
    }

    private static byte[] CopyTable(ReadOnlySpan<byte> src,
        Dictionary<string, (uint offset, uint length)> tables, string tag)
    {
        if (!tables.TryGetValue(tag, out var t)) return [];
        return src.Slice((int)t.offset, (int)t.length).ToArray();
    }

    // ── checksum utilities ────────────────────────────────────────────────────

    private static uint CalcChecksum(ReadOnlySpan<byte> data)
    {
        uint sum = 0;
        int words = data.Length / 4;
        for (int i = 0; i < words; i++)
            sum += BinaryPrimitives.ReadUInt32BigEndian(data.Slice(i * 4, 4));
        int rem = data.Length & 3;
        if (rem > 0)
        {
            uint last = 0;
            for (int b = 0; b < rem; b++)
                last |= (uint)data[words * 4 + b] << (24 - b * 8);
            sum += last;
        }
        return sum;
    }

    private static uint CalcChecksum(byte[] data) => CalcChecksum(data.AsSpan());

    // ── math helpers ─────────────────────────────────────────────────────────

    private static int MaxPow2Le(int n)
    {
        int p = 1;
        while (p * 2 <= n) p *= 2;
        return p;
    }

    private static int Log2(int n)
    {
        int r = 0;
        while (n > 1) { n >>= 1; r++; }
        return r;
    }
}
