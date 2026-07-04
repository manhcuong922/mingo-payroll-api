using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Project;

/// <summary>
/// Kiểm scope xưởng cho thao tác ghi đơn/task (v5): GĐ (ProjectReadAll) làm mọi xưởng;
/// quản lý xưởng chỉ được thao tác đơn/task thuộc xưởng mình trực thuộc (GetDepartmentIdOfUserAsync).
/// Gom vào 1 chỗ để Project*/Task* không lặp logic.
/// </summary>
public static class ProjectScopeGuard
{
    public static async Task EnsureCanWriteAsync(
        int? projectDepartmentId,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct)
    {
        // GĐ / Phó GĐ có quyền toàn cục.
        if (currentUser.HasPermission(Permissions.ProjectReadAll)) return;

        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        if (projectDepartmentId is null || !await authorizer.IsManagerOfAsync(userId, projectDepartmentId.Value, ct))
            throw new ForbiddenAccessException("Bạn chỉ được thao tác đơn thuộc xưởng mình.");
    }

    // Task dùng chung guard này: GĐ (ProjectReadAll) sửa được task NGOÀI xưởng mình.
    public static Task EnsureCanWriteTaskAsync(
        int? taskDepartmentId,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct) =>
        EnsureCanWriteAsync(taskDepartmentId, currentUser, authorizer, ct);
}
