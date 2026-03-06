namespace Muonroi.Core.Abstractions.Interfaces;

public interface ICurrentUserContext
{
    MUserModel? CurrentUser { get; set; }
}
