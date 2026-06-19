namespace Muonroi.Pdf.Internal.Layout.Geometry;

internal readonly struct Rect(float x, float y, float width, float height) : IEquatable<Rect>
{
    public float X { get; } = x;
    public float Y { get; } = y;
    public float Width { get; } = width;
    public float Height { get; } = height;

    public float Right => X + Width;
    public float Bottom => Y + Height;

    public bool Equals(Rect other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public override bool Equals(object? obj) => obj is Rect other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

    public static bool operator ==(Rect left, Rect right) => left.Equals(right);
    public static bool operator !=(Rect left, Rect right) => !left.Equals(right);

    public override string ToString() => $"Rect({X}, {Y}, {Width}, {Height})";
}
