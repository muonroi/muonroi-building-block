using System.Threading.Tasks;
using Muonroi.Pdf.Abstractions;
using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;

namespace Muonroi.Pdf.Tests.Helpers;

/// <summary>
/// Builds a <see cref="BoxTreeBuilder"/> box tree from real HTML through the owned CSS cascade
/// (AngleSharpHtmlParser → AngleSharpStyledDocument → BoxTreeBuilder) — the same path the
/// production render pipeline uses.
///
/// Phase 12 B1.3: replaces the old "FakeStyledNode with empty computed styles + a &lt;style&gt;
/// block" approach for tests that used to exercise the now-deleted BoxTreeBuilder class-rule
/// fallbacks. Those tests are repointed here so they assert the owned cascade resolves the same
/// CSS (descendant selectors, shorthand expansion, inheritance) end-to-end.
/// </summary>
internal static class CascadeBoxTree
{
    public static async Task<BlockBox> BuildAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        IParsedDocument parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var angleParsed = (AngleSharpParsedDocument)parsed;
        var doc = new AngleSharpStyledDocument(angleParsed.Document, angleParsed.SourceHtmlBytes);
        return new BoxTreeBuilder().Build(doc.Root);
    }
}
