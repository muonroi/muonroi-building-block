namespace Muonroi.Core.Abstractions.Models;

/// <summary>
/// Model used for user information in token generation and API responses.
/// For User Update API, use the parameterless constructor.
/// </summary>
public class MUserModel
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization (required for API requests like UpdateUser)
    /// </summary>
    public MUserModel()
    {
        UserGuid = string.Empty;
        Username = string.Empty;
        TokenValidity = string.Empty;
        Name = string.Empty;
        Surname = string.Empty;
        PhoneNumber = string.Empty;
        Email = string.Empty;
    }

    /// <summary>
    /// Full constructor for token generation
    /// </summary>
    public MUserModel(
        string userGuid,
        string username,
        string tokenValidity,
        string name,
        string surname,
        string phoneNumber,
        string email,
        string? tenantId = null)
    {
        UserGuid = userGuid;
        Username = username;
        TokenValidity = tokenValidity;
        Name = name;
        Surname = surname;
        PhoneNumber = phoneNumber;
        Email = email;
        TenantId = tenantId;
    }

    public string UserGuid { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string PhoneNumber { get; set; }
    public string Email { get; set; }
    public string Username { get; set; }
    public string? TenantId { get; set; }
    public string TokenValidity { get; set; }
}
