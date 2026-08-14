namespace Muonroi.Pdf.Tests.Golden;

/// <summary>
/// FLEX-08 opt-in-safety regression guard. The decisive proof that the Phase 18 modern-layout opt-in
/// did NOT perturb the default render path: the default-path corpus (<see cref="GoldenCorpus.AllCases"/>)
/// must hold exactly the pre-phase committed-baseline count, and the flex group must stay excluded.
///
/// Byte-identity of the existing baselines is independently proven by the default-path golden theories
/// (BlockLayoutGoldenTests, InlineLayoutGoldenTests, TableGoldenTests, … all driven by the flag-less
/// VerifyAsync) running green with NO MUONROI_UPDATE_SNAPSHOTS — every default-path case structurally
/// matches its committed baseline. This guard locks the COUNT so a flex case can never silently leak
/// into the default path (T-18-08).
/// </summary>
public sealed class FlexRegressionGuardTests
{
    // FLEX-08 / D-04: the locked invariant is "the default-path corpus count is UNCHANGED from before
    // Phase 18". MEASURED at execution time (2026-06-21): GoldenCorpus.AllCasesData().Count() == 84
    // (the registered default-path cases). This is the quantity that guards against a flex case leaking
    // into AllCases (T-18-08) — it is what AllCasesData() returns and what the flag-less canary +
    // default-path theory iterate.
    //
    // Reconciliation of the "81" / "82" figures in CONTEXT/ROADMAP/FLEX-08:
    //   * 81 = number of committed *.pdf baseline FILES under TestResources/Golden for the default path
    //          (`ls TestResources/Golden/*.pdf | wc -l` == 90 total − 9 new flex = 81 default).
    //   * 84 = number of registered default-path GoldenCase entries in AllCases.
    //   * The 3-case gap: w7-rgb-background-color, w7-transparent-background-no-fill,
    //     w7-float-left-inline-beside are exercised by the determinism canary but ship WITHOUT a
    //     committed .pdf baseline (they have no flag-less VerifyAsync baseline file). So the corpus
    //     count (84) was always 3 higher than the on-disk baseline-file count (81); the upstream "82"
    //     conflated the two. Asserting the corpus count is the correct, stable, evidence-backed guard.
    private const int DefaultPathCorpusCount = 84;

    [Fact]
    public void DefaultPath_Baseline_Count_Unchanged()
    {
        GoldenCorpus.AllCasesData().Count().Should().Be(DefaultPathCorpusCount,
            because: "the modern-layout opt-in must not change the default-path corpus size — "
                + "flex cases live in the standalone FlexLayout group, never in AllCases (FLEX-08, T-18-08)");
    }

    [Fact]
    public void FlexCases_AreExcludedFromDefaultPath()
    {
        var allNames = GoldenCorpus.AllCasesData().Select(d => (string)d[0]).ToHashSet();
        var flexNames = GoldenCorpus.FlexCasesData().Select(d => (string)d[0]).ToList();

        flexNames.Should().NotBeEmpty(because: "the flex golden group must exist (FLEX-07)");
        flexNames.Should().OnlyContain(name => !allNames.Contains(name),
            because: "no flex case may appear in the flag-less default-path corpus (it would throw "
                + "PdfPolicyException on the canary / default-path theory)");
    }

    [Fact]
    public void GridCases_AreExcludedFromDefaultPath()
    {
        // GRID-08 / T-19-08: grid cases live in the standalone GridLayout group, never in AllCases.
        var allNames = GoldenCorpus.AllCasesData().Select(d => (string)d[0]).ToHashSet();
        var gridNames = GoldenCorpus.GridCasesData().Select(d => (string)d[0]).ToList();

        gridNames.Should().NotBeEmpty(because: "the grid golden group must exist (GRID-07)");
        gridNames.Should().OnlyContain(name => !allNames.Contains(name),
            because: "no grid case may appear in the flag-less default-path corpus (it would throw "
                + "PdfPolicyException forbidden.display.grid on the canary / default-path theory)");
    }
}
