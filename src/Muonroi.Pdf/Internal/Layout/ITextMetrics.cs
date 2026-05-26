namespace Muonroi.Pdf.Internal.Layout;

internal interface ITextMetrics
{
    float GetCharWidth(char c, string fontFamily, float fontSize, bool bold, bool italic);
    float GetLineHeight(string fontFamily, float fontSize);
    float GetAscender(string fontFamily, float fontSize);
    float GetDescender(string fontFamily, float fontSize);
}
