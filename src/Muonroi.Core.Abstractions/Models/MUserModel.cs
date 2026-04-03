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

    /// <summary>
    /// The user GUID.
    /// </summary>
    public string UserGuid { get; set; }
    /// <summary>
    /// The user's name.
    /// </summary>
    public string Name { get; set; }
    /// <summary>
    /// The user's surname.
    /// </summary>
    public string Surname { get; set; }
    /// <summary>
    /// The user's phone number.
    /// </summary>
    public string PhoneNumber { get; set; }
    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; set; }
    /// <summary>
    /// The username.
    /// </summary>
    public string Username { get; set; }
    /// <summary>
    /// The tenant ID.
    /// </summary>
    public string? TenantId { get; set; }
    /// <summary>
    /// The token validity.
    /// </summary>
    public string TokenValidity { get; set; }
}
