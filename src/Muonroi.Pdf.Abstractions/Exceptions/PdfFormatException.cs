namespace Muonroi.Pdf.Abstractions.Exceptions;

/// <summary>
/// Thrown when the HTML or CSS input is structurally malformed or contains constructs
/// that cannot be processed by the PDF rendering pipeline.
/// </summary>
/// <param name="ruleId">Policy or validation rule identifier that detected the format violation.</param>
/// <param name="message">Human-readable description of the format problem.</param>
/// <param name="inner">Optional inner exception from the underlying parser or decoder.</param>
public sealed class PdfFormatException(string ruleId, string message, Exception? inner = null) : PdfException(ruleId, message, message, inner)
{
}
