namespace Muonroi.Core.Abstractions.Interfaces;

public interface IPermissionProvider
{
    IEnumerable<PermissionDefinition> GetPermissions();
}

