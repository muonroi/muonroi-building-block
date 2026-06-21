namespace Muonroi.Pdf.Enterprise.Registry;

/// <summary>
/// Configuration for <see cref="HttpPdfTemplateRegistry"/>. The base address is taken from the named
/// <c>HttpClient</c> ("PdfTemplateRegistry") the host registers via <c>AddHttpClient</c>; this only
/// carries cross-cutting options.
/// </summary>
public sealed class PdfTemplateRegistryOptions
{
    /// <summary>The named <see cref="System.Net.Http.HttpClient"/> the registry resolves from the factory.</summary>
    public const string HttpClientName = "PdfTemplateRegistry";

    /// <summary>
    /// Optional bearer-token factory invoked per request when the registry endpoint requires auth.
    /// Returns <see langword="null"/> to send no Authorization header.
    /// </summary>
    public Func<CancellationToken, Task<string?>>? AccessTokenFactory { get; set; }
}
