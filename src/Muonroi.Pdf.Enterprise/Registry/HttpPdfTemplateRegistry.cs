using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Muonroi.Core.Abstractions.Guards;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Pdf.Enterprise.Registry;

/// <summary>
/// HTTP/REST <see cref="IMPdfTemplateRegistry"/> client. This is the building-block side of the
/// building-block ↔ control-plane contract; the control-plane PDF-templates API conforms to it:
/// <list type="bullet">
///   <item><c>GET {base}/api/v1/pdf-templates/{templateId}</c> → 200 <see cref="TemplateDescriptorDto"/>, 404 → not found.</item>
///   <item><c>GET {base}/api/v1/pdf-templates/{templateId}/versions/{version}</c> → 200 <see cref="TemplateVersionDto"/> (content base64), 404 → not found.</item>
/// </list>
/// Push-based change notifications (<see cref="SubscribeAsync"/>) require the control-plane hot-reload
/// transport (SignalR/Redis), which is cross-repo; poll specific templates with
/// <see cref="PdfTemplateHotReload"/> over this registry instead.
/// </summary>
public sealed class HttpPdfTemplateRegistry(
    IHttpClientFactory httpClientFactory,
    PdfTemplateRegistryOptions? options = null,
    IMLog<HttpPdfTemplateRegistry>? logger = null) : IMPdfTemplateRegistry
{
    private readonly PdfTemplateRegistryOptions _options = options ?? new PdfTemplateRegistryOptions();

    /// <inheritdoc/>
    public async Task<TemplateDescriptor?> LookupAsync(string templateId, CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(templateId);

        HttpClient client = httpClientFactory.CreateClient(PdfTemplateRegistryOptions.HttpClientName);
        using HttpRequestMessage request = new(HttpMethod.Get, $"api/v1/pdf-templates/{Uri.EscapeDataString(templateId)}");
        await ApplyAuthAsync(request, cancellationToken);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        TemplateDescriptorDto? dto = await response.Content.ReadFromJsonAsync<TemplateDescriptorDto>(cancellationToken);
        if (dto is null)
            return null;

        return new TemplateDescriptor(dto.TemplateId, dto.Name, dto.LatestVersion, dto.Tags ?? []);
    }

    /// <inheritdoc/>
    public async Task<TemplateVersion?> ResolveAsync(string templateId, string version, CancellationToken cancellationToken = default)
    {
        MGuard.NotEmpty(templateId);
        MGuard.NotEmpty(version);

        HttpClient client = httpClientFactory.CreateClient(PdfTemplateRegistryOptions.HttpClientName);
        using HttpRequestMessage request = new(HttpMethod.Get,
            $"api/v1/pdf-templates/{Uri.EscapeDataString(templateId)}/versions/{Uri.EscapeDataString(version)}");
        await ApplyAuthAsync(request, cancellationToken);

        using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();

        TemplateVersionDto? dto = await response.Content.ReadFromJsonAsync<TemplateVersionDto>(cancellationToken);
        if (dto is null)
            return null;

        byte[] content;
        try
        {
            content = Convert.FromBase64String(dto.ContentBase64 ?? string.Empty);
        }
        catch (FormatException ex)
        {
            logger?.Error(ex, "[PdfRegistry] Template {TemplateId}@{Version} returned invalid base64 content.", templateId, version);
            return null;
        }

        return new TemplateVersion(dto.TemplateId, dto.Version, dto.ContentType, content, dto.PublishedAt);
    }

    /// <inheritdoc/>
    public Task<IAsyncDisposable> SubscribeAsync(IAsyncObserver<TemplateChange> observer, CancellationToken cancellationToken = default)
    {
        // Watch-all push notifications are a control-plane transport concern (SignalR/Redis), which is
        // cross-repo. The building-block side polls specific templates via PdfTemplateHotReload.
        // MSTD0001 suppressed: NotSupportedException is the idiomatic "feature not implemented on this
        // implementation" signal for a public API contract; callers must catch it without taking a
        // Muonroi.Core (MException) dependency.
#pragma warning disable MSTD0001
        throw new NotSupportedException(
            "HttpPdfTemplateRegistry does not provide push change notifications. Poll specific templates " +
            "with PdfTemplateHotReload, or use the control-plane hot-reload transport for watch-all push.");
#pragma warning restore MSTD0001
    }

    private async Task ApplyAuthAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_options.AccessTokenFactory is null)
            return;

        string? token = await _options.AccessTokenFactory(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Wire DTO for a template descriptor.</summary>
    public sealed record TemplateDescriptorDto(string TemplateId, string Name, string LatestVersion, List<string>? Tags);

    /// <summary>Wire DTO for a resolved template version; content is base64-encoded.</summary>
    public sealed record TemplateVersionDto(string TemplateId, string Version, string ContentType, string? ContentBase64, DateTimeOffset PublishedAt);
}
