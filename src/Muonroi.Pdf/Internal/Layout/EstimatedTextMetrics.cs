namespace Muonroi.Pdf.Internal.Layout;

internal sealed class EstimatedTextMetrics : ITextMetrics
{
    public static readonly EstimatedTextMetrics Instance = new();

    public float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic)
        => fontSize * 0.6f;

    public float GetLineHeight(string fontFamily, float fontSize)
        => fontSize * 1.2f;

    public float GetAscender(string fontFamily, float fontSize)
        => fontSize * 0.8f;

    public float GetDescender(string fontFamily, float fontSize)
        => fontSize * 0.2f;
}
