using Muonroi.Pdf.Internal.Layout;

namespace Muonroi.Pdf.Tests.Layout;

public sealed class FloatPlacementSolverTests
{
    private static readonly ContainingBlock CB = new(X: 0f, Width: 100f);

    // -------------------------------------------------------------------------
    // AvoidCollisions — basic placement
    // -------------------------------------------------------------------------

    [Fact]
    public void LeftFloat_Single()
    {
        // No existing floats — left-float placed at cb.X
        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 30f, boxHeight: 20f,
            side: FloatSide.Left, cb: CB, exclusions: []);

        result.X.Should().Be(0f);
        result.Y.Should().Be(0f);
        result.AvailableWidth.Should().Be(100f);
    }

    [Fact]
    public void LeftFloat_Stack_Horizontal()
    {
        // First left-float at X=0, width=30. Second left-float should start at X=30.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 20f, Side: FloatSide.Left)
        ];

        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 40f, boxHeight: 20f,
            side: FloatSide.Left, cb: CB, exclusions: exclusions);

        result.X.Should().Be(30f);
        result.Y.Should().Be(0f);
        result.AvailableWidth.Should().Be(70f); // 100 - 30
    }

    [Fact]
    public void LeftFloat_DropToNextRow()
    {
        // First left-float is 70 wide. Second left-float is also 70 wide — doesn't fit same row.
        // Second should drop below first (Y=20).
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 70f, Bottom: 20f, Side: FloatSide.Left)
        ];

        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 70f, boxHeight: 15f,
            side: FloatSide.Left, cb: CB, exclusions: exclusions);

        result.Y.Should().Be(20f); // advanced to bottom of first float
        result.X.Should().Be(0f);
    }

    [Fact]
    public void RightFloat_Single()
    {
        // Single right-float placed at cb.X + cb.Width - boxWidth = 100 - 30 = 70
        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 30f, boxHeight: 20f,
            side: FloatSide.Right, cb: CB, exclusions: []);

        result.X.Should().Be(70f);
        result.Y.Should().Be(0f);
        result.AvailableWidth.Should().Be(100f);
    }

    [Fact]
    public void RightFloat_Stack()
    {
        // First right-float at X=70 (width=30). Second right-float (width=20) stacks left of it → X=50.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 70f, Top: 0f, Right: 100f, Bottom: 20f, Side: FloatSide.Right)
        ];

        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 20f, boxHeight: 20f,
            side: FloatSide.Right, cb: CB, exclusions: exclusions);

        result.X.Should().Be(50f); // minRight=70, 70-20=50
        result.Y.Should().Be(0f);
        result.AvailableWidth.Should().Be(70f); // minRight(70) - maxLeft(0)
    }

    [Fact]
    public void MixedFloats_AvailWidth()
    {
        // Left float occupies [0,30), right float occupies [80,100).
        // Available width for a new float at Y=0 should be 80-30=50.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 20f, Side: FloatSide.Left),
            new FloatExclusion(Left: 80f, Top: 0f, Right: 100f, Bottom: 20f, Side: FloatSide.Right)
        ];

        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 50f, boxHeight: 10f,
            side: FloatSide.Left, cb: CB, exclusions: exclusions);

        result.AvailableWidth.Should().Be(50f);
        result.X.Should().Be(30f);
        result.Y.Should().Be(0f);
    }

    [Fact]
    public void TallLeft_ShortRight_DifferentBands()
    {
        // Tall left float: [0,60) height=40. Short right-float (width=30, height=10) at Y=0
        // can't fit because left float takes [0,60) at X=[0,60) and cb=100 → available=40 ≥ 30.
        // Right float should be placed in same band.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 60f, Bottom: 40f, Side: FloatSide.Left)
        ];

        var result = FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 30f, boxHeight: 10f,
            side: FloatSide.Right, cb: CB, exclusions: exclusions);

        // minRight stays 100, maxLeft pushed to 60. Available=40 ≥ 30 → fits at Y=0
        result.Y.Should().Be(0f);
        result.X.Should().Be(70f); // minRight(100) - 30
        result.AvailableWidth.Should().Be(40f);
    }

    // -------------------------------------------------------------------------
    // AvailableWidthAtY
    // -------------------------------------------------------------------------

    [Fact]
    public void AvailableWidthAtY_NoExclusions()
    {
        var result = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 10f, lineHeight: 12f, cb: CB, exclusions: []);

        result.StartX.Should().Be(0f);
        result.AvailableWidth.Should().Be(100f);
    }

    [Fact]
    public void AvailableWidthAtY_WithFloats()
    {
        // Left float [0,40) height=20 at Y=0; right float [75,100) height=20 at Y=0.
        // Query at lineY=5, lineHeight=10 → both overlap.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 40f, Bottom: 20f, Side: FloatSide.Left),
            new FloatExclusion(Left: 75f, Top: 0f, Right: 100f, Bottom: 20f, Side: FloatSide.Right)
        ];

        var result = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 5f, lineHeight: 10f, cb: CB, exclusions: exclusions);

        result.StartX.Should().Be(40f);
        result.AvailableWidth.Should().Be(35f); // 75 - 40
    }

    // -------------------------------------------------------------------------
    // AvailableWidthAtY — lineHeight=0 degeneracy (8.12a regression guard)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Demonstrates that lineHeight=0f produces a degenerate result (no exclusion detected)
    /// while the corrected non-zero lineHeight correctly narrows the available width.
    ///
    /// Scenario: float exclusion spans Y=[50,80] on the left, width=30.
    /// Query at lineY=70, lineHeight=20: band=[70,90] overlaps [50,80] → exclusion IS detected.
    /// Query at lineY=70, lineHeight=0f: band=[70,70] (empty) → exclusion NOT detected (degenerate).
    /// </summary>
    [Fact]
    public void AvailableWidthAtY_LineHeightZero_DegenenerateVsCorrect()
    {
        // Left float exclusion spans Y=[50,80], X=[0,30)
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 50f, Right: 30f, Bottom: 80f, Side: FloatSide.Left)
        ];

        // Correct call: lineY=70, lineHeight=20 → band=[70,90] overlaps [50,80] → exclusion detected
        var correct = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 70f, lineHeight: 20f, cb: CB, exclusions: exclusions);

        correct.StartX.Should().Be(30f,  "left float at [50,80] overlaps the [70,90] line band");
        correct.AvailableWidth.Should().Be(70f, "100 - 30 = 70 after left exclusion");

        // Degenerate call: lineHeight=0f → band=[70,70] (zero height) → overlap test fails for all exclusions
        // ex.Top(50) >= bandBottom(70)? No. ex.Bottom(80) <= bandTop(70)? No.
        // Wait — actually with lineHeight=0: bandBottom=70, bandTop=70.
        // ex.Top(50) >= 70? No. ex.Bottom(80) <= 70? No. → exclusion IS seen (both conditions false).
        // But: ex.Top >= bandBottom: 50 >= 70 → false. ex.Bottom <= bandTop: 80 <= 70 → false.
        // The overlap condition passes even for zero height here because the exclusion straddles lineY.
        // The degeneracy occurs when lineY is BELOW the exclusion top and the line would extend INTO it.
        // Demonstrate with lineY=79, lineHeight=0 (line top at bottom of exclusion, band=[79,79]).
        // Without height the band doesn't reach up to verify overlap correctly.

        // Case 2: lineY=79 (just inside bottom of exclusion), lineHeight=2 → band=[79,81] overlaps [50,80]
        var atBottom = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 79f, lineHeight: 2f, cb: CB, exclusions: exclusions);
        atBottom.StartX.Should().Be(30f, "line starting at 79 with height 2 still overlaps [50,80]");

        // Case 3: lineY=79, lineHeight=0 → band=[79,79]; ex.Bottom(80)<=79? No; ex.Top(50)>=79? No → still detected
        // True degeneracy: line starts AFTER exclusion end but exclusion would be missed with non-zero height
        // Demonstrate: lineY=49, lineHeight=2 → band=[49,51] overlaps [50,80] (bandBottom=51 > ex.Top=50)
        var partialOverlapCorrect = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 49f, lineHeight: 2f, cb: CB, exclusions: exclusions);
        partialOverlapCorrect.StartX.Should().Be(30f, "line [49,51] overlaps exclusion top at 50");
        partialOverlapCorrect.AvailableWidth.Should().Be(70f);

        // Degenerate: lineY=49, lineHeight=0 → band=[49,49]; ex.Top(50)>=49? No; ex.Bottom(80)<=49? No
        // Both false → exclusion IS seen even with lineHeight=0 when lineY is within the exclusion.
        // The true degenerate case: line starts exactly at exclusion top, height=0.
        // lineY=50, lineHeight=0 → band=[50,50]; ex.Bottom(80)<=50? No; ex.Top(50)>=50? YES → skipped!
        var trueDegeneracy = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 50f, lineHeight: 0f, cb: CB, exclusions: exclusions);
        trueDegeneracy.StartX.Should().Be(0f,  "degenerate: lineHeight=0 misses exclusion whose Top==lineY");
        trueDegeneracy.AvailableWidth.Should().Be(100f, "degenerate: full width reported, no exclusion detected");

        // Fixed: lineY=50, lineHeight=12 → band=[50,62] overlaps [50,80]
        var fixed50 = FloatPlacementSolver.AvailableWidthAtY(
            lineY: 50f, lineHeight: 12f, cb: CB, exclusions: exclusions);
        fixed50.StartX.Should().Be(30f,  "correct lineHeight detects exclusion starting at lineY");
        fixed50.AvailableWidth.Should().Be(70f);
    }

    // -------------------------------------------------------------------------
    // ClearY
    // -------------------------------------------------------------------------

    [Fact]
    public void ClearY_Left()
    {
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 25f, Side: FloatSide.Left),
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 40f, Side: FloatSide.Left),
            new FloatExclusion(Left: 70f, Top: 0f, Right: 100f, Bottom: 50f, Side: FloatSide.Right)
        ];

        FloatPlacementSolver.ClearY(FloatSide.Left, exclusions).Should().Be(40f);
    }

    [Fact]
    public void ClearY_Right()
    {
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 40f, Side: FloatSide.Left),
            new FloatExclusion(Left: 70f, Top: 0f, Right: 100f, Bottom: 50f, Side: FloatSide.Right),
            new FloatExclusion(Left: 70f, Top: 0f, Right: 100f, Bottom: 30f, Side: FloatSide.Right)
        ];

        FloatPlacementSolver.ClearY(FloatSide.Right, exclusions).Should().Be(50f);
    }

    [Fact]
    public void ClearY_Both()
    {
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 40f, Side: FloatSide.Left),
            new FloatExclusion(Left: 70f, Top: 0f, Right: 100f, Bottom: 60f, Side: FloatSide.Right)
        ];

        FloatPlacementSolver.ClearY(null, exclusions).Should().Be(60f);
    }

    [Fact]
    public void ClearY_EmptyList()
    {
        FloatPlacementSolver.ClearY(null, []).Should().Be(0f);
    }

    [Fact]
    public void ClearY_Left_TwoFloatsDifferentHeights()
    {
        // Two left-floats with different bottoms; ClearY(Left) returns the max bottom.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 15f, Side: FloatSide.Left),
            new FloatExclusion(Left: 0f, Top: 0f, Right: 20f, Bottom: 35f, Side: FloatSide.Left)
        ];

        FloatPlacementSolver.ClearY(FloatSide.Left, exclusions).Should().Be(35f);
    }

    [Fact]
    public void ClearY_Both_OneLeftOneRight()
    {
        // One left float (bottom=22) and one right float (bottom=45).
        // ClearY(null) — clear:both — returns max of both sides = 45.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 25f, Bottom: 22f, Side: FloatSide.Left),
            new FloatExclusion(Left: 75f, Top: 0f, Right: 100f, Bottom: 45f, Side: FloatSide.Right)
        ];

        FloatPlacementSolver.ClearY(null, exclusions).Should().Be(45f);
    }

    [Fact]
    public void ClearY_Right_NoRightFloats()
    {
        // Only left floats present; ClearY(Right) finds no right exclusions → returns 0.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 30f, Bottom: 50f, Side: FloatSide.Left)
        ];

        FloatPlacementSolver.ClearY(FloatSide.Right, exclusions).Should().Be(0f);
    }

    // -------------------------------------------------------------------------
    // Infinite-loop guard
    // -------------------------------------------------------------------------

    [Fact]
    public void AvoidCollisions_InfiniteLoopGuard()
    {
        // Degenerate: entire cb width is consumed by overlapping floats at every Y level.
        // Left float takes [0,60), right float takes [40,100) — no gap anywhere.
        // Both have the same Bottom so advancing candidateY doesn't help.
        List<FloatExclusion> exclusions =
        [
            new FloatExclusion(Left: 0f, Top: 0f, Right: 60f, Bottom: 99999f, Side: FloatSide.Left),
            new FloatExclusion(Left: 40f, Top: 0f, Right: 100f, Bottom: 99999f, Side: FloatSide.Right)
        ];

        // Should not loop forever; should return something (not throw)
        var act = () => FloatPlacementSolver.AvoidCollisions(
            candidateY: 0f, boxWidth: 50f, boxHeight: 20f,
            side: FloatSide.Left, cb: CB, exclusions: exclusions);

        act.Should().NotThrow();
    }
}
