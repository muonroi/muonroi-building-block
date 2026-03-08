namespace Muonroi.AuthZ.Policies;

/// <summary>
/// Provides authorization services using Open Policy Agent (OPA).
/// </summary>
/// <param name="httpClient">The HTTP client to communicate with the OPA service.</param>
/// <param name="policyPath">The path to the OPA policy for authorization decisions.</param>
public class OpaAuthorizationService(HttpClient httpClient, string policyPath = "/v1/data/authz/allow")
{
    /// <summary>
    /// Authorizes a request based on the provided input and OPA policy.
    /// </summary>
    /// <param name="input">The input data for the authorization policy.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>True if authorized; otherwise, false.</returns>
    public async Task<bool> AuthorizeAsync(object input, CancellationToken cancellationToken = default)
    {
        try
        {
            HttpResponseMessage response = await httpClient.PostAsJsonAsync(policyPath, new { input }, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;

            OpaResponse? result = await response.Content.ReadFromJsonAsync<OpaResponse>(cancellationToken);
            return result?.Result ?? false;
        }
        catch
        {
            return false;
        }
    }

    private sealed record OpaResponse([property: JsonPropertyName("result")] bool Result);
}