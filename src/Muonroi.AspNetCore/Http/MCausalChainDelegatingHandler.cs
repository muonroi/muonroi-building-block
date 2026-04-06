using Microsoft.Extensions.Options;
using Muonroi.Core.Abstractions.Diagnostics;
using System.Diagnostics;
using System.Net.Http;

namespace Muonroi.AspNetCore.Http;

/// <summary>
/// A <see cref="DelegatingHandler"/> that reads the <c>muonroi-causal-chain</c> response header
/// from upstream HTTP error responses and stores the chain in <see cref="Activity.Current"/> baggage
/// under the key <c>muonroi.causal</c>.
///
/// This enables <see cref="Muonroi.Core.Abstractions.Exceptions.MException"/> instances thrown
/// in the current service to automatically carry the upstream chain via their
/// <c>CausalChain</c> property (per D-07).
///
/// Behavior:
/// <list type="bullet">
///   <item><description>Non-2xx response + header present → chain deserialized and stored in baggage.</description></item>
///   <item><description>2xx response → header ignored (success path is not a chain carrier).</description></item>
///   <item><description>Header absent or invalid base64/JSON → baggage not set; no exception thrown.</description></item>
///   <item><description>No <see cref="Activity.Current"/> → silently skipped.</description></item>
/// </list>
/// </summary>
public class MCausalChainDelegatingHandler(IOptions<MCausalChainOptions> options) : DelegatingHandler
{
    private readonly MCausalChainOptions _options = options.Value;

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode &&
            response.Headers.TryGetValues("muonroi-causal-chain", out IEnumerable<string>? values))
        {
            string? headerValue = values.FirstOrDefault();
            if (!string.IsNullOrEmpty(headerValue))
            {
                // Validate chain can be deserialized before storing in baggage.
                // TryDeserialize returns null for invalid base64, invalid JSON, or chains exceeding maxDepth.
                MCausalChain? chain = MCausalChain.TryDeserialize(headerValue, _options.MaxChainDepth);
                if (chain is not null)
                {
                    Activity.Current?.SetBaggage("muonroi.causal", headerValue);
                }
            }
        }

        return response;
    }
}
