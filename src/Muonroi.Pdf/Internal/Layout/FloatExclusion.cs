namespace Muonroi.Pdf.Internal.Layout;

/// <summary>
/// An immutable bounding rect for a placed float, stored in the BFC exclusion list.
/// Mirrors WeasyPrint's placed float record used in avoid_collisions (float.py ~line 43).
/// All coordinates are in points, absolute page space within the current BFC.
/// </summary>
internal readonly record struct FloatExclusion(
    float Left,
    float Top,
    float Right,
    float Bottom,
    FloatSide Side
);
