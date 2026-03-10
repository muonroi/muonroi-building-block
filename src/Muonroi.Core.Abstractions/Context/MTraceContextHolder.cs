using Muonroi.Core.Abstractions.Diagnostics;

namespace Muonroi.Core.Abstractions.Context;

/// <summary>
/// Holds the active trace session in an AsyncLocal context.
/// Internal to prevent direct manipulation outside of proper scopes.
/// </summary>
internal static class MTraceContextHolder
{
    internal static readonly AsyncLocal<ITraceSession?> Current = new();
}
