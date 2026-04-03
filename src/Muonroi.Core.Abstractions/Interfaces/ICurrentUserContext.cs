namespace Muonroi.Core.Abstractions.Interfaces;

/// <summary>
/// Provides access to the current user's context.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Gets or sets the current user model.
    /// </summary>
    MUserModel? CurrentUser { get; set; }
}
