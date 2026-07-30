using MPS.Application.Common.Interfaces;
using MPS.Application.Services;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;

namespace MPS.Application.Features.Project;

/// <summary>
/// Kiểm scope xưởng + vai trò cho thao tác ghi đơn/task (refactor R/S). Hai nhánh quyền:
///   - "Lập kế hoạch" (tạo/sửa/xoá task, gán người, generate, tạo/sửa đơn, sửa timeline, pause/cancel):
///     Planning Manager của xưởng (IsManagerOfAsync) hoặc GĐ/Phó GĐ (ProjectReadAll).
///   - "Tiến độ" (UpdateProgress, Complete task, ProjectComplete): Tổ trưởng sản xuất đang phụ trách đơn
///     (Project.ResponsibleUserId) HOẶC Planning Manager của xưởng / GĐ override.
/// Đã BỎ hoàn toàn per-stage PM (AssignedPmUserId) — không còn lọc/ẩn theo PM công đoạn.
/// </summary>
public static class ProjectScopeGuard
{
    // Permission-based, không hardcode role: bất kỳ role nào được gán ProjectReadFullDepartment/
    // TaskReadFullDepartment (vd. Planning Manager) đều xem trọn cây của xưởng mình.
    public static bool CanSeeFullDepartmentProjects(ICurrentUserService currentUser) =>
        currentUser.HasPermission(Permissions.ProjectReadAll)
        || currentUser.HasPermission(Permissions.ProjectReadFullDepartment);

    public static bool CanSeeFullDepartmentTasks(ICurrentUserService currentUser) =>
        currentUser.HasPermission(Permissions.TaskReadAll)
        || currentUser.HasPermission(Permissions.TaskReadFullDepartment);

    // === Nhánh "lập kế hoạch" — Planning Manager của xưởng / GĐ ===

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

    // Sửa cấu trúc/gán người/pause/cancel trên MỌI node (container/leaf/gate) = Planning Manager của xưởng / GĐ.
    // (refactor: bỏ nhánh per-stage PM — không còn "chỉ PM phụ trách công đoạn cha".)
    public static Task EnsureCanPlanTaskAsync(
        ProjectTaskEntity task,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct) =>
        EnsureCanWriteTaskAsync(task.DepartmentId, currentUser, authorizer, ct);

    // === Nhánh "tiến độ" — Tổ trưởng sản xuất (ResponsibleUserId) / Planning Manager / GĐ ===

    public static async Task EnsureCanUpdateProgressAsync(
        ProjectTaskEntity task,
        IProjectRepository projects,
        ICurrentUserService currentUser,
        IDepartmentManagerAuthorizer authorizer,
        CancellationToken ct)
    {
        if (currentUser.HasPermission(Permissions.ProjectReadAll)) return; // GĐ/Phó GĐ override

        var userId = currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        // Tổ trưởng sản xuất đang phụ trách đơn được cập nhật tiến độ / hoàn thành.
        if (task.ProjectId is int projectId)
        {
            var project = await projects.GetByIdAsync(projectId, ct);
            if (project?.ResponsibleUserId == userId) return;

            // Planning Manager của xưởng đơn được override.
            if (project?.DepartmentId is int deptId && await authorizer.IsManagerOfAsync(userId, deptId, ct)) return;
        }
        else if (task.DepartmentId is int taskDeptId && await authorizer.IsManagerOfAsync(userId, taskDeptId, ct))
        {
            // Task nội bộ (không thuộc đơn) — chỉ Planning Manager của xưởng.
            return;
        }

        throw new ForbiddenAccessException(
            "Chỉ tổ trưởng sản xuất phụ trách đơn (hoặc Planning Manager/GĐ) mới cập nhật được tiến độ.");
    }

    // === Đọc task ===

    /// <summary>
    /// Công nhân (Task.Read.Assigned) có task trong đơn X → được ĐỌC mọi task của đơn X (xem cả việc
    /// của đồng nghiệp trong cùng đơn để nắm bối cảnh dây chuyền). Đơn không tham gia vẫn bị chặn,
    /// và task IsConfidential vẫn lọc riêng ở đầu EnsureCanReadTaskAsync.
    /// </summary>
    public static async Task<bool> CanReadTasksOfJoinedProjectAsync(
        int? projectId,
        IProjectTaskRepository tasks,
        ICurrentUserService currentUser,
        CancellationToken ct)
    {
        if (projectId is null) return false;                                  // task nội bộ, không thuộc đơn
        if (!currentUser.HasPermission(Permissions.TaskReadAssigned)) return false;
        if (currentUser.UserId is not int userId) return false;

        return await tasks.HasAssignedTaskInProjectAsync(userId, projectId.Value, ct);
    }

    // ReadAll → mọi task; ReadFullDepartment/ReadDepartment + là quản lý xưởng → trọn cây xưởng;
    // ReadAssigned + là assignee → task được giao, HOẶC task thuộc đơn mình có tham gia
    // (CanReadTasksOfJoinedProjectAsync). (refactor: bỏ lọc theo AssignedPmUserId.)
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

        var canReadDepartment = currentUser.HasPermission(Permissions.TaskReadFullDepartment)
            || currentUser.HasPermission(Permissions.TaskReadDepartment);
        if (canReadDepartment && isManagerOfTaskDepartment) return;

        if (currentUser.HasPermission(Permissions.TaskReadAssigned) && isAssignee) return;

        // Công nhân xem task của đồng nghiệp trong đơn mình cùng làm.
        if (await CanReadTasksOfJoinedProjectAsync(task.ProjectId, tasks, currentUser, ct)) return;

        throw new ForbiddenAccessException("Bạn không có quyền xem task này.");
    }
}
