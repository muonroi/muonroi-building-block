namespace Muonroi.Core.Abstractions.Interfaces;

public interface IAuthContextFactory
{
    IAuthenticateInfoContext Create();
}
