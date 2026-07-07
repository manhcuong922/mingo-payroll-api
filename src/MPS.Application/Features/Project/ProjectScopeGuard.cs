using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;

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

    // v7 mục 8: tạo Công đoạn (IsBatchParent=true) → chỉ Manager của xưởng / GĐ.
    // Tạo Task lá (IsBatchParent=false) → chỉ PM đang được giao phụ trách Công đoạn cha / GĐ.
    public static async Task EnsureCanCreateTaskAsync(
        bool isBatchParent,
        int? departmentId,
        int? assignedPmUserIdOfParent,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct)
    {
        if (currentUser.HasPermission(Permissions.ProjectReadAll)) return; // GĐ/Phó GĐ toàn quyền

        if (isBatchParent)
        {
            await EnsureCanWriteAsync(departmentId, currentUser, authorizer, ct);
            return;
        }

        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");
        if (assignedPmUserIdOfParent != userId)
            throw new ForbiddenAccessException("Chỉ PM đang phụ trách công đoạn này mới được tạo task con.");
    }

    // v7 mục 8: ghi (sửa/hoàn thành/cập nhật sản lượng/pause/cancel/resume/restore) trên Công đoạn
    // → Manager của xưởng/GĐ. Trên Task lá → CHỈ đúng PM đang phụ trách Công đoạn cha/GĐ — không phải
    // "bất kỳ PM nào cùng xưởng" (khác EnsureCanWriteTaskAsync, vốn chỉ kiểm scope xưởng).
    public static async Task EnsureCanWriteTaskLeafAsync(
        ProjectTaskEntity task,
        IProjectTaskRepository tasks,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct)
    {
        if (task.IsBatchParent)
        {
            await EnsureCanWriteTaskAsync(task.DepartmentId, currentUser, authorizer, ct);
            return;
        }

        if (currentUser.HasPermission(Permissions.ProjectReadAll)) return; // GĐ/Phó GĐ toàn quyền

        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var parentPm = task.ParentTaskId is int parentId
            ? (await tasks.GetByIdAsync(parentId, ct))?.AssignedPmUserId
            : null;
        if (parentPm != userId)
            throw new ForbiddenAccessException("Chỉ PM đang phụ trách công đoạn này mới được thao tác task con.");
    }
}
