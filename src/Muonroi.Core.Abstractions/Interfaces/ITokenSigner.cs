namespace Muonroi.Core.Abstractions.Interfaces;

public interface ITokenSigner
{
    SigningCredentials GetCredentials();
}
