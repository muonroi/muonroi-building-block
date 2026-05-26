namespace Muonroi.Pdf.Abstractions.Engine;

public interface ICssCascadeEngine
{
    ValueTask<IStyledDocument> CascadeAsync(IParsedDocument doc, string? userStyleSheet, CancellationToken ct = default);
}
