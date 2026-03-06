namespace Muonroi.AspNetCore.Services;

public interface IPermissionService<TPermission>
    where TPermission : Enum
{
    Task<MResponse<MRole>> CreateRoleAsync(CreateRoleRequestModel request, CancellationToken cancellationToken);
    Task<MResponse<object>> AssignPermissionToRoleAsync(AssignPermissionRequestModel request, CancellationToken cancellationToken);
    Task<MResponse<object>> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken);
    Task<MResponse<object>> AssignRoleToUserAsync(AssignRoleRequestModel request, CancellationToken cancellationToken);
    Task<MResponse<List<TPermission>>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken);
    Task<MResponse<MRole>> UpdateRoleAsync(UpdateRoleRequestModel request, CancellationToken cancellationToken);
    Task<MResponse<object>> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken);
    Task<MResponse<List<MRole>>> GetRolesAsync(CancellationToken cancellationToken);
    Task<MResponse<List<MPermission>>> GetPermissionsAsync(CancellationToken cancellationToken);
    Task<MResponse<List<MPermission>>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken);
    Task<MResponse<List<MUser>>> GetRoleUsersAsync(Guid roleId, CancellationToken cancellationToken);
    Task<MResponse<List<PermissionDefinition>>> GetPermissionDefinitionsAsync(CancellationToken cancellationToken);
    Task<MResponse<List<MenuMetadata>>> GetMenuMetadataAsync(Guid userId, CancellationToken cancellationToken);
    Task<MResponse<MUiManifest>> GetUiManifestAsync(Guid userId, CancellationToken cancellationToken);
    Task<MResponse<MUiEngineManifest>> GetUiEngineManifestAsync(Guid userId, CancellationToken cancellationToken);
    Task<MResponse<MUiEngineContractInfo>> GetUiEngineContractInfoAsync(CancellationToken cancellationToken);
    Task<MResponse<MUiEngineSchemaVersion>> GetUiEngineSchemaVersionAsync(CancellationToken cancellationToken);
    Task<MResponse<object>> NotifyUiEngineSchemaChangeAsync(MUiEngineSchemaChangeNotification notification, CancellationToken cancellationToken);
    Task<MResponse<PermissionTree>> GetUserPermissionTreeAsync(Guid userId, CancellationToken cancellationToken);
    Task<MResponse<MPagedResult<MUser>>> GetUsersAsync(int pageIndex, int pageSize, CancellationToken cancellationToken);
    Task<MResponse<MUser>> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<MResponse<MUser>> UpdateUserAsync(MUserModel request, CancellationToken cancellationToken);
    Task<MResponse<object>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
}
