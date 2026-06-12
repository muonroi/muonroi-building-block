namespace Muonroi.Pdf.Abstractions.Engine;

public interface IHtmlParser
{
    ValueTask<IParsedDocument> ParseAsync(string html, CancellationToken ct = default);
}
