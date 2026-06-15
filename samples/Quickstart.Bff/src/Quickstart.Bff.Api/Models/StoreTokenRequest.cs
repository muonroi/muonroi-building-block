namespace Quickstart.Bff.Api.Models;

/// <summary>
/// Request body for storing a server-side refresh token against a subject.
/// </summary>
public record StoreTokenRequest(string Subject, string RefreshToken);
