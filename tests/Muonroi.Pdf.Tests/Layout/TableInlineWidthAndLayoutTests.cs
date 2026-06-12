using Muonroi.Pdf.Governance.Cascade;
using Muonroi.Pdf.Governance.Parsing;
using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// G23 regression tests: BoxTreeBuilder correctly resolves table-layout from class rules
/// and width from inline style="..." attribute when GetComputedStyle fails (% widths, no viewport).
///
/// Root causes (see RESEARCH-G23.md):
///   Defect 1 — table-layout not in class-rule fallback whitelist → auto mode instead of fixed.
///   Defect 3 — &lt;th style="width:16%"&gt; inline-style width lost when GetComputedStyle fails.
/// </summary>
public sealed class TableInlineWidthAndLayoutTests
{
    // -------------------------------------------------------------------------
    // Helper: parse HTML through real AngleSharp → BoxTreeBuilder end-to-end.
    // This exercises the actual GetComputedStyle failure path that the unit tests
    // in TableCellPercentWidthTests bypass by injecting pre-built box objects.
    // -------------------------------------------------------------------------
    private static async Task<BlockBox> BuildFromHtmlAsync(string html)
    {
        var parser = new AngleSharpHtmlParser();
        var cascade = new AngleSharpCascadeEngine();
        var parsed = await parser.ParseAsync(html).ConfigureAwait(false);
        var styled = await cascade.CascadeAsync(parsed, userStyleSheet: null).ConfigureAwait(false);
        return new BoxTreeBuilder().Build(styled.Root);
    }

    // -------------------------------------------------------------------------
    // Collect all BoxNode descendants of a given type.
    // -------------------------------------------------------------------------
    private static List<T> CollectAll<T>(BoxNode root) where T : BoxNode
    {
        var result = new List<T>();
        CollectAllRecursive(root, result);
        return result;
    }

    private static void CollectAllRecursive<T>(BoxNode node, List<T> result) where T : BoxNode
    {
        if (node is T match) result.Add(match);
        foreach (var child in node.Children)
            CollectAllRecursive(child, result);
    }

    // -------------------------------------------------------------------------
    // Case 1: table-layout class-rule fallback (Defect 1)
    //
    // HTML: <table class="t"> where .t { table-layout: fixed; width: 100% }
    // Expected: table box has TableLayout == "fixed"
    // -------------------------------------------------------------------------
    [Fact]
    public async Task TableLayout_FromClassRule_IsSetToFixed_WhenComputedStyleFails()
    {
        const string html = @"<!DOCTYPE html>
<html><head>
<style>.t { table-layout: fixed; width: 100%; }</style>
</head><body>
<table class=""t"">
  <tr>
    <th style=""width:16%"">A</th>
    <th style=""width:14%"">B</th>
  </tr>
</table>
</body></html>";

        var root = await BuildFromHtmlAsync(html).ConfigureAwait(false);

        var tables = CollectAll<TableBox>(root);
        tables.Should().NotBeEmpty(because: "HTML contains a <table> element");
        var table = tables[0];

        table.TableLayout.Should().Be("fixed",
            because: ".t { table-layout: fixed } must be resolved from the class-rule fallback " +
                     "even when GetComputedStyle fails due to width:100% with no viewport");
    }

    // -------------------------------------------------------------------------
    // Case 2: inline-style width attribute fallback for <th> cells (Defect 3)
    //
    // HTML: <table class="t"> with <th style="width:16%"> and <th style="width:14%">
    // Expected: each cell has WidthRaw set to "16%" and "14%" respectively
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ThCells_InlineStyleWidth_PreservedAsWidthRaw_WhenComputedStyleFails()
    {
        const string html = @"<!DOCTYPE html>
<html><head>
<style>.t { table-layout: fixed; width: 100%; }</style>
</head><body>
<table class=""t"">
  <tr>
    <th style=""width:16%"">A</th>
    <th style=""width:14%"">B</th>
  </tr>
</table>
</body></html>";

        var root = await BuildFromHtmlAsync(html).ConfigureAwait(false);

        var cells = CollectAll<TableCellBox>(root);
        cells.Should().HaveCount(2,
            because: "HTML contains exactly two <th> cells");

        cells[0].WidthRaw.Should().Be("16%",
            because: "first <th style=\"width:16%\"> must have WidthRaw populated via " +
                     "inline-style attribute fallback when GetComputedStyle returns empty");

        cells[1].WidthRaw.Should().Be("14%",
            because: "second <th style=\"width:14%\"> must have WidthRaw populated via " +
                     "inline-style attribute fallback when GetComputedStyle returns empty");
    }

    // -------------------------------------------------------------------------
    // Case 3: Combined — both Defect 1 + Defect 3 together (regression guard)
    //
    // Verifies that with both fixes applied, a table matching the CHNG_E pattern
    // has: (a) TableLayout=="fixed", (b) all five percent-width cells populated.
    // -------------------------------------------------------------------------
    [Fact]
    public async Task ChngE_TablePattern_TableLayoutFixed_AndAllCellWidthsPreserved()
    {
        const string html = @"<!DOCTYPE html>
<html><head>
<style>.table-bodered2 { table-layout: fixed; width: 100%; border-collapse: collapse; }</style>
</head><body>
<table class=""table-bodered2"">
  <thead>
    <tr>
      <th style=""width: 16%;"">Số Container</th>
      <th style=""width: 16%;"">Loại Container</th>
      <th style=""width: 10%;"">Seal No</th>
      <th style=""width: 14%;"">Gross Weight</th>
      <th style=""width: 14%;"">CBM</th>
      <th style=""width: 12%;"">Số Kiện</th>
      <th style=""width: 18%;"">Ghi chú</th>
    </tr>
  </thead>
  <tbody>
    <tr>
      <td>TGHU1234567</td>
      <td>45G1</td>
      <td>SL001</td>
      <td>10000</td>
      <td>25.5</td>
      <td>100</td>
      <td></td>
    </tr>
  </tbody>
</table>
</body></html>";

        var root = await BuildFromHtmlAsync(html).ConfigureAwait(false);

        var tables = CollectAll<TableBox>(root);
        tables.Should().NotBeEmpty();
        tables[0].TableLayout.Should().Be("fixed",
            because: "CHNG_E pattern must resolve table-layout:fixed from class rule");

        var cells = CollectAll<TableCellBox>(root);
        // Header row has 7 <th> cells; body row has 7 <td> cells.
        var headerCells = cells.Take(7).ToList();

        headerCells[0].WidthRaw.Should().Be("16%", because: "first <th> must have WidthRaw=16%");
        headerCells[1].WidthRaw.Should().Be("16%", because: "second <th> must have WidthRaw=16%");
        headerCells[2].WidthRaw.Should().Be("10%", because: "third <th> must have WidthRaw=10%");
        headerCells[3].WidthRaw.Should().Be("14%", because: "fourth <th> must have WidthRaw=14%");
        headerCells[4].WidthRaw.Should().Be("14%", because: "fifth <th> must have WidthRaw=14%");
    }

    // -------------------------------------------------------------------------
    // Case 4: non-regression — elements with working computed-style must NOT
    // be affected by the inline-style fallback (i.e. fallback only activates
    // when computed style AND class rules both return empty for width).
    // -------------------------------------------------------------------------
    [Fact]
    public async Task RegularDiv_WithExplicitPixelWidth_NotAffectedByInlineStyleFallback()
    {
        // A <div> with no % in its style — GetComputedStyle should work fine.
        // The inline-style fallback must not interfere.
        const string html = @"<!DOCTYPE html>
<html><body>
<div style=""width: 200px;"">content</div>
</body></html>";

        var root = await BuildFromHtmlAsync(html).ConfigureAwait(false);

        // The div will produce some BlockBox; we verify it resolves width correctly.
        // Width of 200px → in points: 200 * 0.75 = 150pt.
        var divBoxes = CollectAll<BlockBox>(root)
            .Where(b => b.Source?.LocalName == "div")
            .ToList();

        // We don't assert exact point values (unit conversion tested elsewhere),
        // just that WidthRaw is populated and Width is not -1f sentinel:
        divBoxes.Should().NotBeEmpty(because: "HTML contains a <div> element");
        // At least one div should have its width resolved (not sentinel -1f for %).
        divBoxes.Should().Contain(b => b.WidthRaw != null && b.Width > 0f,
            because: "200px width must resolve to a positive point value");
    }
}
