namespace Muonroi.AspNetCore.Services;

/// <summary>
/// Defines a service for managing permissions, roles, and user-role associations.
/// </summary>
/// <typeparam name="TPermission">The type of the permission enum.</typeparam>
public interface IPermissionService<TPermission>
    where TPermission : Enum
{
    /// <summary>
    /// Creates a new role asynchronously.
    /// </summary>
    /// <param name="request">The create role request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MRole}"/> containing the created role.</returns>
    Task<MResponse<MRole>> CreateRoleAsync(CreateRoleRequestModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns a permission to a role asynchronously.
    /// </summary>
    /// <param name="request">The assign permission request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    Task<MResponse<object>> AssignPermissionToRoleAsync(AssignPermissionRequestModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a permission from a role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="permissionId">The unique identifier of the permission.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    Task<MResponse<object>> RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns a role to a user asynchronously.
    /// </summary>
    /// <param name="request">The assign role request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    Task<MResponse<object>> AssignRoleToUserAsync(AssignRoleRequestModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the permissions granted to a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{TPermission}}"/> containing the user's permissions.</returns>
    Task<MResponse<List<TPermission>>> GetUserPermissionsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing role asynchronously.
    /// </summary>
    /// <param name="request">The update role request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MRole}"/> containing the updated role.</returns>
    Task<MResponse<MRole>> UpdateRoleAsync(UpdateRoleRequestModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    Task<MResponse<object>> DeleteRoleAsync(Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all roles asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MRole}}"/> containing all roles.</returns>
    Task<MResponse<List<MRole>>> GetRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets all permissions asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MPermission}}"/> containing all permissions.</returns>
    Task<MResponse<List<MPermission>>> GetPermissionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets permissions associated with a specific role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MPermission}}"/> containing the role's permissions.</returns>
    Task<MResponse<List<MPermission>>> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets users associated with a specific role asynchronously.
    /// </summary>
    /// <param name="roleId">The unique identifier of the role.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MUser}}"/> containing the role's users.</returns>
    Task<MResponse<List<MUser>>> GetRoleUsersAsync(Guid roleId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets definitions for all available permissions asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{PermissionDefinition}}"/> containing permission definitions.</returns>
    Task<MResponse<List<PermissionDefinition>>> GetPermissionDefinitionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets menu metadata for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{List{MenuMetadata}}"/> containing menu metadata.</returns>
    Task<MResponse<List<MenuMetadata>>> GetMenuMetadataAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the UI manifest for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiManifest}"/> containing the UI manifest.</returns>
    Task<MResponse<MUiManifest>> GetUiManifestAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the UI engine manifest for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiEngineManifest}"/> containing the UI engine manifest.</returns>
    Task<MResponse<MUiEngineManifest>> GetUiEngineManifestAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the UI engine contract information asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiEngineContractInfo}"/> containing contract information.</returns>
    Task<MResponse<MUiEngineContractInfo>> GetUiEngineContractInfoAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the UI engine schema version asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUiEngineSchemaVersion}"/> containing the schema version.</returns>
    Task<MResponse<MUiEngineSchemaVersion>> GetUiEngineSchemaVersionAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Notifies the UI engine of a schema change asynchronously.
    /// </summary>
    /// <param name="notification">The schema change notification details.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result.</returns>
    Task<MResponse<object>> NotifyUiEngineSchemaChangeAsync(MUiEngineSchemaChangeNotification notification, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the permission tree for a specific user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{PermissionTree}"/> containing the user's permission tree.</returns>
    Task<MResponse<PermissionTree>> GetUserPermissionTreeAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a paged list of users asynchronously.
    /// </summary>
    /// <param name="pageIndex">The page index (1-based).</param>
    /// <param name="pageSize">The number of users per page.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MPagedResult{MUser}}"/> containing the paged list of users.</returns>
    Task<MResponse<MPagedResult<MUser>>> GetUsersAsync(int pageIndex, int pageSize, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a user by their unique identifier asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUser}"/> containing the user details.</returns>
    Task<MResponse<MUser>> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates user information asynchronously.
    /// </summary>
    /// <param name="request">The user update request model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{MUser}"/> containing the updated user details.</returns>
    Task<MResponse<MUser>> UpdateUserAsync(MUserModel request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a user asynchronously.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="MResponse{Object}"/> indicating the result of the operation.</returns>
    Task<MResponse<object>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);
}
