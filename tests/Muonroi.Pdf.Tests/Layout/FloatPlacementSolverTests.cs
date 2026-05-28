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
