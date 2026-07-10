using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using ProjectEntity = MPS.Domain.Entities.Project;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;
using TaskAssignee = MPS.Domain.Entities.TaskAssignee;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskUpdateCommandHandler : IRequestHandler<TaskUpdateCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskUpdateCommandHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IDepartmentRepository departments,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _departments = departments;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskUpdateCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdWithGraphAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanPlanTaskAsync(task, _currentUser, _authorizer, ct);

        // Load 1 lần dùng chung cho validate deadline + recompute ratio (tránh N+1 khi 2 nhánh cùng cần).
        var project = task.ProjectId is int pid ? await _projects.GetByIdAsync(pid, ct) : null;

        ValidateDeadlineScope(request, task, project, await LoadParentIfNeededAsync(task, ct));

        // Sản lượng scale tự do — KHÔNG chặn "con vượt cha" nữa (% tiến độ do trọng số quyết định, không do sản lượng).
        // Container cha có PlannedQuantity=0 nên luật cũ luôn chặn ⇒ không gán được người. Đã bỏ hẳn.

        task.Title = request.Title.Trim();
        task.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        task.Deadline = request.Deadline;
        // ⭐ Khoá vị trí tương đối theo timeline đơn để lần đổi StartDate/Deadline sau vẫn giữ ý người dùng
        // — nếu không, cascade recompute-from-ratio sẽ dùng ratio cũ và wipe deadline mới.
        task.DeadlineOffsetRatio = project is null
            ? null
            : TaskDeadlineCalculator.RatioFromDeadline(project.StartDate, project.Deadline, request.Deadline);
        task.Priority = request.Priority;

        if (task.IsBatchParent)
        {
            task.PlannedQuantity = request.PlannedQuantity;
            task.PlannedWorkUnits = request.PlannedWorkUnits;
        }
        else if (task.ProgressType == ProgressType.Quantity)
        {
            task.PlannedQuantity = request.PlannedQuantity;
        }
        else
        {
            // Gate (Milestone) — không có sản lượng để sửa, chỉ đổi tính bắt buộc.
            task.IsRequiredGate = request.IsRequiredGate;
        }

        // Giải quyết + validate danh sách người gán TRƯỚC khi xoá khỏi graph (guard cần danh sách hiện tại).
        var desiredAssignees = await ResolveAssigneesAsync(request, task, ct);

        // Không để UpdateAsync (DbSet.Update toàn graph) đụng vào dòng gán — TaskAssignee khóa ghép sẽ bị coi
        // là Modified và UPDATE 0 dòng. Sync riêng qua ReplaceAssigneesAsync (delete + insert đúng state).
        task.Assignees = new List<TaskAssignee>();
        var updated = await _tasks.UpdateAsync(task, ct);

        if (desiredAssignees is not null)
            await _tasks.ReplaceAssigneesAsync(task.Id, desiredAssignees, VietnamClock.Now, ct);

        return updated;
    }

    // Deadline task không được vượt phạm vi cha (công đoạn) / đơn. project + parent load sẵn ngoài (tránh N+1).
    private static void ValidateDeadlineScope(
        TaskUpdateCommand request, ProjectTaskEntity task, ProjectEntity? project, ProjectTaskEntity? parent)
    {
        if (request.Deadline is not { } newDeadline) return;

        if (parent?.Deadline is { } parentDeadline && newDeadline > parentDeadline)
            throw new DomainValidationException("Deadline task con không được vượt quá deadline công đoạn cha.");

        if (project?.Deadline is { } projectDeadline && newDeadline > projectDeadline)
            throw new DomainValidationException("Deadline task không được vượt quá deadline đơn.");
    }

    private async Task<ProjectTaskEntity?> LoadParentIfNeededAsync(ProjectTaskEntity task, CancellationToken ct) =>
        task.ParentTaskId is int parentId ? await _tasks.GetByIdAsync(parentId, ct) : null;

    // Validate danh sách người gán (nếu gửi) & trả về id đã distinct để sync. null = không đổi assignee.
    // Cùng xưởng + đúng chức danh + không InProgress ở task khác (refactor I/P/D).
    private async Task<IReadOnlyList<int>?> ResolveAssigneesAsync(
        TaskUpdateCommand request, ProjectTaskEntity task, CancellationToken ct)
    {
        if (request.AssigneeUserIds is not { } newAssignees) return null;

        if (task.IsBatchParent)
        {
            if (newAssignees.Count > 0)
                throw new DomainValidationException("Node gộp (container) không được giao người trực tiếp.");
            return Array.Empty<int>();
        }

        var currentAssigneeIds = task.Assignees.Select(a => a.UserId).ToHashSet();
        await TaskAssigneeGuard.ValidateAsync(
            newAssignees, task.DepartmentId!.Value, task.RequiredPositionId,
            currentAssigneeIds, _departments, _tasks, ct);

        return newAssignees.Distinct().ToList();
    }
}
