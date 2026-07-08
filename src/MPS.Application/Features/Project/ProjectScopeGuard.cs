using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;

namespace MPS.Application.Features.Project;

// Rely on entity graph fields only (Assignees/DepartmentId/ParentTaskId) so callers only need
// GetByIdWithGraphAsync — no extra repository roundtrip beyond the optional parent-PM lookup.

/// <summary>
/// Kiểm scope xưởng cho thao tác ghi đơn/task (v5): GĐ (ProjectReadAll) làm mọi xưởng;
/// quản lý xưởng chỉ được thao tác đơn/task thuộc xưởng mình trực thuộc (GetDepartmentIdOfUserAsync).
/// Gom vào 1 chỗ để Project*/Task* không lặp logic.
/// </summary>
public static class ProjectScopeGuard
{
    // Permission-based, không hardcode role: bất kỳ role nào được gán ProjectReadFullDepartment/
    // TaskReadFullDepartment (vd. DepartmentManager) đều xem trọn 3 tầng của xưởng mình, bỏ qua bộ lọc
    // theo AssignedPmUserId — giống ProjectReadAll/TaskReadAll nhưng giới hạn trong 1 xưởng thay vì toàn công ty.
    public static bool CanSeeFullDepartmentProjects(ICurrentUserService currentUser) =>
        currentUser.HasPermission(Permissions.ProjectReadAll)
        || currentUser.HasPermission(Permissions.ProjectReadFullDepartment);

    public static bool CanSeeFullDepartmentTasks(ICurrentUserService currentUser) =>
        currentUser.HasPermission(Permissions.TaskReadAll)
        || currentUser.HasPermission(Permissions.TaskReadFullDepartment);

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

    // Cùng scope "ai được xem task" như TaskGetByIdQueryHandler (ReadAll/ReadDepartment+PM scope/ReadAssigned)
    // — dùng chung để CommentGetByOwnerQueryHandler không lặp lại (và không lệch) logic đọc task.
    public static async Task EnsureCanReadTaskAsync(
        ProjectTaskEntity task,
        IProjectTaskRepository tasks,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct)
    {
        var isAssignee = currentUser.UserId is { } uid && task.Assignees.Any(a => a.UserId == uid);

        if (task.IsConfidential
            && !currentUser.HasPermission(Permissions.TaskReadConfidential)
            && currentUser.UserId != task.CreatedByUserId
            && !isAssignee)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem task nhạy cảm này.");
        }

        if (currentUser.HasPermission(Permissions.TaskReadAll)) return;

        var userId = currentUser.UserId;
        var isManagerOfTaskDepartment = userId is not null && task.DepartmentId is not null
            && await authorizer.IsManagerOfAsync(userId.Value, task.DepartmentId.Value, ct);

        // TaskReadFullDepartment (vd. DepartmentManager): xem trọn 3 tầng của xưởng mình, bỏ qua lọc PM.
        if (currentUser.HasPermission(Permissions.TaskReadFullDepartment) && isManagerOfTaskDepartment) return;

        // TaskReadDepartment (PM tổ): chỉ Công đoạn/task con được gán cho chính mình.
        if (currentUser.HasPermission(Permissions.TaskReadDepartment) && isManagerOfTaskDepartment
            && userId is not null && await IsVisibleToPmAsync(task, userId.Value, tasks, ct))
        {
            return;
        }

        if (currentUser.HasPermission(Permissions.TaskReadAssigned) && isAssignee) return;

        throw new ForbiddenAccessException("Bạn không có quyền xem task này.");
    }

    // Công đoạn/task con đã gán cho PM khác (khác userId hiện tại) → ẩn khỏi PM này.
    // Chưa gán ai (AssignedPmUserId null) vẫn hiện — không thuộc về ai để giấu.
    private static async Task<bool> IsVisibleToPmAsync(
        ProjectTaskEntity task, int userId, IProjectTaskRepository tasks, CancellationToken ct)
    {
        int? assignedPm;
        if (task.IsBatchParent)
        {
            assignedPm = task.AssignedPmUserId;
        }
        else if (task.ParentTaskId is int parentId)
        {
            var parent = await tasks.GetByIdAsync(parentId, ct);
            assignedPm = parent?.AssignedPmUserId;
        }
        else
        {
            assignedPm = null;
        }

        return assignedPm is null || assignedPm == userId;
    }
}
