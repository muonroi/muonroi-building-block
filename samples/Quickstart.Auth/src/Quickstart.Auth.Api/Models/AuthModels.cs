namespace Quickstart.Auth.Api.Models;

/// <summary>Request to issue a JWT for a subject (user id / username).</summary>
public sealed record TokenRequest(string Subject, int LifetimeMinutes = 60);

/// <summary>Request carrying a JWT to validate or revoke.</summary>
public sealed record TokenEnvelope(string Token);

/// <summary>Request to hash + verify a password using BCrypt.</summary>
public sealed record PasswordRequest(string Password);
