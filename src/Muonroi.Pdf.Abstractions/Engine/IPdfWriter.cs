using Muonroi.Pdf.Abstractions;

namespace Muonroi.Pdf.Abstractions.Engine;

public interface IPdfWriter
{
    ValueTask<long> WriteAsync(
        IPositionedPageList pages,
        PdfRenderOptions options,
        Stream destination,
        CancellationToken ct = default);
}
