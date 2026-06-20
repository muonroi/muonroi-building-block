using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Muonroi.Logging.Abstractions;

namespace Muonroi.Pdf.Internal.Font;

/// <summary>
/// Default <see cref="IFontResolver"/> that reads font registrations from
/// <c>PdfConfigs:FontResolver</c> and resolves requests using a four-step fallback chain:
/// <list type="number">
///   <item>Exact Family + Weight + Style match.</item>
///   <item>Family-only match (any weight/style — first registered wins).</item>
///   <item>Generic-family map lookup (e.g. "serif" → "Times New Roman") then recurse.</item>
///   <item>First registered font, if <see cref="PdfFontResolverConfig.FallbackToFirstRegistered"/> is true.</item>
/// </list>
/// Font bytes are loaded once and cached per unique file path. All access is thread-safe.
/// </summary>
internal sealed class DefaultFontResolver : IFontResolver
{
    // ── key: "{family}|{weight}|{style}" (OrdinalIgnoreCase family) → Lazy file load ──
    private readonly List<(string Family, int Weight, FontStyle Style, Lazy<ReadOnlyMemory<byte>?> Bytes)> _registry;
    private readonly PdfFontResolverConfig _config;
    private readonly IMLog<DefaultFontResolver> _logger;

    // De-duplicate file reads: multiple entries can reference the same TTF path.
    private readonly ConcurrentDictionary<string, Lazy<ReadOnlyMemory<byte>?>> _fileCache =
        new(StringComparer.OrdinalIgnoreCase);

    public DefaultFontResolver(
        IOptions<PdfConfigs> options,
        IHostEnvironment hostEnvironment,
        IMLog<DefaultFontResolver> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(hostEnvironment);
        ArgumentNullException.ThrowIfNull(logger);

        _config = options.Value.FontResolver;
        _logger = logger;

        string contentRoot = hostEnvironment.ContentRootPath ?? AppContext.BaseDirectory;

        _registry = new List<(string, int, FontStyle, Lazy<ReadOnlyMemory<byte>?>)>(_config.Fonts.Count);

        foreach (PdfFontEntry entry in _config.Fonts)
        {
            if (string.IsNullOrWhiteSpace(entry.Family) || string.IsNullOrWhiteSpace(entry.Path))
                continue;

            // Resolve relative path against ContentRoot.
            string absolutePath = System.IO.Path.IsPathRooted(entry.Path)
                ? entry.Path
                : System.IO.Path.GetFullPath(System.IO.Path.Combine(contentRoot, entry.Path));

            // Capture for closure — do NOT capture loop variable.
            string capturedPath = absolutePath;
            string capturedFamily = entry.Family;

            Lazy<ReadOnlyMemory<byte>?> lazyBytes = _fileCache.GetOrAdd(
                absolutePath,
                _ => new Lazy<ReadOnlyMemory<byte>?>(() => LoadFile(capturedPath, capturedFamily), LazyThreadSafetyMode.ExecutionAndPublication));

            _registry.Add((entry.Family, entry.Weight, entry.Style, lazyBytes));
        }
    }

    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> ResolveAsync(FontRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        // Step 1: exact match (Family + Weight + Style).
        foreach (var (family, weight, style, lazy) in _registry)
        {
            if (string.Equals(family, request.Family, StringComparison.OrdinalIgnoreCase)
                && weight == (int)request.Weight
                && style == request.Style)
            {
                return new ValueTask<ReadOnlyMemory<byte>?>(lazy.Value);
            }
        }

        // Step 2: family-only match (first registered, any weight/style).
        foreach (var (family, _, _, lazy) in _registry)
        {
            if (string.Equals(family, request.Family, StringComparison.OrdinalIgnoreCase))
            {
                return new ValueTask<ReadOnlyMemory<byte>?>(lazy.Value);
            }
        }

        // Step 3: generic-family map → recurse once on the mapped name.
        if (_config.GenericFamilyMap.TryGetValue(request.Family, out string? mappedFamily)
            && !string.IsNullOrEmpty(mappedFamily)
            && !string.Equals(mappedFamily, request.Family, StringComparison.OrdinalIgnoreCase))
        {
            FontRequest mapped = request with { Family = mappedFamily };
            return ResolveAsync(mapped, cancellationToken);
        }

        // Step 4: first-registered fallback.
        if (_config.FallbackToFirstRegistered && _registry.Count > 0)
        {
            _logger.LogWarning(
                "DefaultFontResolver: no match for family '{Family}' (weight={Weight}, style={Style}); " +
                "falling back to first registered font '{Fallback}'.",
                request.Family, request.Weight, request.Style,
                _registry[0].Family);

            return new ValueTask<ReadOnlyMemory<byte>?>(_registry[0].Bytes.Value);
        }

        // Truly empty registry or fallback disabled.
        return new ValueTask<ReadOnlyMemory<byte>?>((ReadOnlyMemory<byte>?)null);
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private ReadOnlyMemory<byte>? LoadFile(string absolutePath, string familyName)
    {
        if (!File.Exists(absolutePath))
        {
            _logger.LogWarning(
                "DefaultFontResolver: font file for '{Family}' not found at '{Path}'. " +
                "That family will not be available for rendering.",
                familyName, absolutePath);
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            return (ReadOnlyMemory<byte>)bytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex,
                "DefaultFontResolver: failed to read font file for '{Family}' at '{Path}'.",
                familyName, absolutePath);
            return null;
        }
    }
}
