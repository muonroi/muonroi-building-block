namespace Muonroi.AspNetCore.Services;

public sealed class UiManifestBuilder
{
    public static MUiManifest BuildUiManifest(
        Guid userId,
        string? tenantId,
        IReadOnlyList<MPermission> permissions,
        IReadOnlySet<Guid> grantedPermissionIds)
    {
        Dictionary<Guid, MUiManifestItem> itemsById = [];
        foreach (MPermission permission in permissions)
        {
            bool isGranted = grantedPermissionIds.Contains(permission.EntityId);
            bool isPublished = permission.IsGranted;

            itemsById[permission.EntityId] = new MUiManifestItem
            {
                PermissionName = permission.Name,
                UiKey = permission.UiKey,
                ParentUiKey = permission.ParentUiKey,
                Type = permission.Type,
                DisplayName = permission.Label ?? permission.Name,
                Icon = permission.Icon,
                Description = permission.Description,
                Order = permission.Order ?? 0,
                Route = MUiRouteBuilder.Build(permission.UiKey),
                IsPublished = isPublished,
                IsGranted = isGranted,
                IsVisible = isPublished && isGranted,
                IsEnabled = isPublished && isGranted,
                DisabledReason = ResolveDisabledReason(isPublished, isGranted)
            };
        }

        foreach (MPermission permission in permissions.Where(x => x.ParentId.HasValue))
        {
            if (!permission.ParentId.HasValue ||
                !itemsById.TryGetValue(permission.ParentId.Value, out MUiManifestItem? parent) ||
                !itemsById.TryGetValue(permission.EntityId, out MUiManifestItem? child))
            {
                continue;
            }

            child.ParentUiKey ??= parent.UiKey;
            parent.Children.Add(child);
        }

        foreach (MUiManifestItem item in itemsById.Values)
        {
            item.Children = [.. item.Children
                .OrderBy(x => x.Order)
                .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)];
        }

        MUiManifest manifest = new()
        {
            UserId = userId,
            TenantId = tenantId
        };

        var groupedRoots = permissions
            .Where(x => !x.ParentId.HasValue)
            .Select(x => new
            {
                Permission = x,
                Item = itemsById[x.EntityId]
            })
            .GroupBy(x => new
            {
                GroupName = x.Permission.PermissionGroup?.Name ?? string.Empty,
                GroupDisplayName = x.Permission.PermissionGroup?.DisplayName ?? string.Empty
            })
            .OrderBy(x => x.Key.GroupDisplayName, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groupedRoots)
        {
            List<MUiManifestItem> rootItems = [.. group
                .Select(x => x.Item)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.UiKey, StringComparer.OrdinalIgnoreCase)];

            foreach (MUiManifestItem root in rootItems)
            {
                ApplyContainerVisibility(root);
            }

            manifest.Groups.Add(new MUiManifestGroup
            {
                GroupName = group.Key.GroupName,
                GroupDisplayName = group.Key.GroupDisplayName,
                Items = rootItems
            });
        }

        return manifest;
    }

    private static string? ResolveDisabledReason(bool isPublished, bool isGranted)
    {
        if (!isPublished)
        {
            return "permission_unpublished";
        }

        if (!isGranted)
        {
            return "permission_denied";
        }

        return null;
    }

    private static bool ApplyContainerVisibility(MUiManifestItem item)
    {
        if (item.Children.Count == 0)
        {
            return item.IsVisible;
        }

        bool hasVisibleChild = false;
        foreach (var _ in from MUiManifestItem child in item.Children
                          where ApplyContainerVisibility(child)
                          select new { })
        {
            hasVisibleChild = true;
        }

        if ((item.Type == PermissionType.Menu || item.Type == PermissionType.Tab) &&
            item.IsPublished &&
            hasVisibleChild)
        {
            item.IsVisible = true;
        }

        return item.IsVisible;
    }
}
