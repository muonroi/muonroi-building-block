namespace Muonroi.Core.Abstractions.Models.Common.Requests.Login;

/// <summary>
/// Login request model.
/// Claims are generated server-side based on user permissions from database.
/// Client-provided claims have been removed for security reasons (privilege escalation risk).
/// </summary>
public class LoginRequestModel
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}
