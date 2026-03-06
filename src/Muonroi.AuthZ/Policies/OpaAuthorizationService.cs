namespace Muonroi.AuthZ.Policies;

public class OpaAuthorizationService(HttpClient httpClient, string policyPath = "/v1/data/authz/allow")
{
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