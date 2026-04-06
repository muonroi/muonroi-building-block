using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Muonroi.Core.Abstractions.Exceptions;

namespace Muonroi.Core.Abstractions.Diagnostics;

/// <summary>
/// Represents the cross-service causal chain for error propagation.
/// Carries originating service identity, error code, architectural layer,
/// optional message, and a recursive reference to the upstream cause.
///
/// Transport: base64-encoded JSON, propagated via W3C baggage key <c>muonroi.causal</c>.
/// </summary>
public sealed class MCausalChain
{
    /// <summary>Gets the name of the service where the error originated.</summary>
    [JsonPropertyName("originService")]
    public string OriginService { get; init; } = string.Empty;

    /// <summary>Gets the machine-readable error code from the origin service.</summary>
    [JsonPropertyName("originErrorCode")]
    public string OriginErrorCode { get; init; } = string.Empty;

    /// <summary>Gets the architectural layer of the origin service.</summary>
    [JsonPropertyName("originLayer")]
    public MExceptionLayer OriginLayer { get; init; }

    /// <summary>
    /// Gets the optional error message from the origin service.
    /// Only populated when <see cref="MCausalChainOptions.IncludeMessageInChain"/> is <see langword="true"/>.
    /// </summary>
    [JsonPropertyName("originMessage")]
    public string? OriginMessage { get; init; }

    /// <summary>Gets the upstream causal chain (recursive), or <see langword="null"/> if this is the root cause.</summary>
    [JsonPropertyName("cause")]
    public MCausalChain? Cause { get; init; }

    /// <summary>Gets the UTC timestamp when this chain entry was created.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Calculates the depth of this chain (1-based).
    /// A chain with no <see cref="Cause"/> has depth 1; each nested <see cref="Cause"/> adds 1.
    /// </summary>
    /// <returns>The number of nodes in the chain.</returns>
    public int Depth()
    {
        int depth = 1;
        MCausalChain? current = Cause;
        while (current is not null)
        {
            depth++;
            current = current.Cause;
        }
        return depth;
    }

    /// <summary>
    /// Serializes this chain to a base64-encoded UTF-8 JSON string.
    /// Uses <see cref="MCausalChainJsonContext"/> for AOT-safe source-generated serialization.
    /// </summary>
    /// <param name="chain">The chain instance to serialize.</param>
    /// <returns>A base64 string suitable for W3C baggage transport.</returns>
    public static string Serialize(MCausalChain chain)
    {
        string json = JsonSerializer.Serialize(chain, MCausalChainJsonContext.Default.MCausalChain);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Attempts to deserialize a base64-encoded JSON string back to a <see cref="MCausalChain"/>.
    /// Returns <see langword="null"/> on any failure — invalid base64, invalid JSON, or depth exceeding <paramref name="maxDepth"/>.
    /// Per D-03: chains exceeding <paramref name="maxDepth"/> are rejected (not truncated).
    /// </summary>
    /// <param name="base64">The base64-encoded JSON string from W3C baggage.</param>
    /// <param name="maxDepth">Maximum allowed chain depth. Defaults to 10.</param>
    /// <returns>The deserialized chain, or <see langword="null"/> if invalid or too deep.</returns>
    public static MCausalChain? TryDeserialize(string base64, int maxDepth = 10)
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            string json = Encoding.UTF8.GetString(bytes);
            MCausalChain? chain = JsonSerializer.Deserialize(json, MCausalChainJsonContext.Default.MCausalChain);
            if (chain is null)
            {
                return null;
            }

            return chain.Depth() <= maxDepth ? chain : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// AOT-friendly JSON serializer context for <see cref="MCausalChain"/>.
/// Null properties are omitted from the serialized output.
/// </summary>
[JsonSerializable(typeof(MCausalChain))]
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class MCausalChainJsonContext : JsonSerializerContext { }
