using Muonroi.Pdf.Internal.Layout;
using Muonroi.Pdf.Internal.Layout.Boxes;
using Muonroi.Pdf.Internal.Layout.Geometry;

namespace Muonroi.Pdf.Tests.Layout;

/// <summary>
/// Phase 13: PaginationEngine stamping of full-HTML running header/footer — 3-column placement,
/// per-page counter substitution, footer band translation, and ShowLine separator rules.
/// </summary>
public sealed class RunningHeaderFooterTests
{
    private static List<PositionedElement> TwoPageBody() => new()
    {
        new() { Source = new InlineBox { Text = "a" }, RenderedText = "a", Position = new Rect(10, 0, 20, 50) },
        new() { Source = new InlineBox { Text = "b" }, RenderedText = "b", Position = new Rect(10, 120, 20, 50) },
    };

    private static RenderedRunningContent SampleRunning()
    {
        var rc = new RenderedRunningContent
        {
            HeaderBandPt = 20f,
            FooterBandPt = 20f,
            HeaderShowLine = true,
            FooterShowLine = true,
            LineColor = "#888888",
            ContentLeftPt = 10f,
            ContentWidthPt = 400f,
        };
        rc.HeaderElements.Add(new PositionedElement
        {
            Source = new InlineBox { Text = "counter(page)/counter(pages)" },
            RenderedText = "counter(page)/counter(pages)",
            Position = new Rect(300f, 5f, 50f, 12f),
        });
        rc.FooterElements.Add(new PositionedElement
        {
            Source = new InlineBox { Text = "bachtx" },
            RenderedText = "bachtx",
            Position = new Rect(10f, 2f, 40f, 12f),
        });
        return rc;
    }

    private static bool IsSeparator(PositionedElement e) =>
        e.Source is InlineBox ib && string.IsNullOrEmpty(ib.Text) && ib.BackgroundColor is { Length: > 0 };

    [Fact]
    public void Header_counter_is_substituted_per_page()
    {
        var pages = new PaginationEngine().Paginate(
            TwoPageBody(), pageBodyHeight: 100f, pageTopMarginPt: 20f, pageBottomMarginPt: 20f,
            pageWidth: 420f, pageHeight: 800f, totalPages: 2, running: SampleRunning());

        pages.Pages.Should().HaveCount(2, because: "body element at Y=120 overflows the 100pt body band");

        pages.Pages[0].Elements.Should().Contain(e => e.RenderedText == "1/2",
            because: "counter(page)/counter(pages) on page 1 of 2 → '1/2'");
        pages.Pages[1].Elements.Should().Contain(e => e.RenderedText == "2/2",
            because: "counter(page)/counter(pages) on page 2 of 2 → '2/2'");
    }

    [Fact]
    public void Header_and_footer_appear_on_every_page()
    {
        var pages = new PaginationEngine().Paginate(
            TwoPageBody(), 100f, 20f, 20f, 420f, 800f, totalPages: 2, running: SampleRunning());

        foreach (var page in pages.Pages)
        {
            page.Elements.Should().Contain(e => e.RenderedText == "bachtx",
                because: "footer text repeats on every page");
            page.Elements.Should().Contain(e => e.RenderedText != null && e.RenderedText.EndsWith("/2"),
                because: "header page counter repeats on every page");
        }
    }

    [Fact]
    public void Footer_element_is_translated_into_bottom_band()
    {
        var pages = new PaginationEngine().Paginate(
            TwoPageBody(), 100f, 20f, 20f, 420f, 800f, totalPages: 2, running: SampleRunning());

        // footerTop = pageHeight − FooterBandPt = 800 − 20 = 780; element band-local Y=2 → 782.
        var footer = pages.Pages[0].Elements.Single(e => e.RenderedText == "bachtx");
        footer.Position.Y.Should().BeApproximately(782f, 0.01f);
    }

    [Fact]
    public void Header_element_stays_in_top_band()
    {
        var pages = new PaginationEngine().Paginate(
            TwoPageBody(), 100f, 20f, 20f, 420f, 800f, totalPages: 2, running: SampleRunning());

        var hdr = pages.Pages[0].Elements.Single(e => e.RenderedText == "1/2");
        hdr.Position.Y.Should().BeApproximately(5f, 0.01f, because: "header band starts at page top Y=0");
        hdr.Position.X.Should().BeApproximately(300f, 0.01f, because: "right column X offset is preserved");
    }

    [Fact]
    public void ShowLine_emits_a_separator_rect_for_header_and_footer()
    {
        var pages = new PaginationEngine().Paginate(
            TwoPageBody(), 100f, 20f, 20f, 420f, 800f, totalPages: 2, running: SampleRunning());

        var seps = pages.Pages[0].Elements.Where(IsSeparator).ToList();
        seps.Should().HaveCount(2, because: "one header rule + one footer rule per page");

        seps.Should().Contain(e => System.Math.Abs(e.Position.Y - (20f - 0.7f)) < 0.01f,
            because: "header separator sits at HeaderBandPt − thickness");
        seps.Should().Contain(e => System.Math.Abs(e.Position.Y - 780f) < 0.01f,
            because: "footer separator sits at pageHeight − FooterBandPt");
        seps.Should().AllSatisfy(e => e.Position.Width.Should().BeApproximately(400f, 0.01f));
    }

    [Fact]
    public void No_running_content_leaves_pages_untouched()
    {
        var pages = new PaginationEngine().Paginate(
            TwoPageBody(), 100f, 20f, 20f, 420f, 800f, totalPages: 2, running: null);

        pages.Pages.Should().HaveCount(2);
        pages.Pages.SelectMany(p => p.Elements).Should().NotContain(e => IsSeparator(e));
    }
}
