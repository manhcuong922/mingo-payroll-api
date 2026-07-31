namespace MPS.Application.Features.Notifications.Recipients;

/// <summary>
/// Logic thuần: gộp Users + Roles + Departments (hoặc AllUsers) qua danh bạ,
/// distinct rồi trừ ExcludeUserIds. Không phụ thuộc hạ tầng cụ thể (chỉ qua INotificationRecipientDirectory).
/// </summary>
public sealed class RecipientResolver : IRecipientResolver
{
    private readonly INotificationRecipientDirectory _directory;

    public RecipientResolver(INotificationRecipientDirectory directory)
    {
        _directory = directory;
    }

    public async Task<IReadOnlyCollection<int>> ResolveAsync(RecipientSpec spec, CancellationToken ct = default)
    {
        if (spec is null || spec.IsEmpty) return Array.Empty<int>();

        var result = new HashSet<int>();

        if (spec.AllUsers)
        {
            foreach (var id in await _directory.GetAllActiveUserIdsAsync(ct)) result.Add(id);
        }
        else
        {
            foreach (var id in spec.UserIds) result.Add(id);

            if (spec.RoleIds.Count > 0)
                foreach (var id in await _directory.GetUserIdsByRolesAsync(spec.RoleIds, ct)) result.Add(id);

            if (spec.DepartmentIds.Count > 0)
                foreach (var id in await _directory.GetUserIdsByDepartmentsAsync(spec.DepartmentIds, ct)) result.Add(id);

            if (spec.PermissionCodes.Count > 0)
                foreach (var id in await _directory.GetUserIdsByPermissionsAsync(spec.PermissionCodes, ct)) result.Add(id);
        }

        foreach (var excluded in spec.ExcludeUserIds) result.Remove(excluded);

        return result;
    }
}
