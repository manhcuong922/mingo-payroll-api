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

// Tạo task thủ công (ngoài generate). Lập kế hoạch = Planning Manager của xưởng / GĐ (refactor: bỏ per-stage PM).
public class TaskCreateCommandHandler : IRequestHandler<TaskCreateCommand, int>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IDepartmentRepository _departments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskCreateCommandHandler(
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

    public async Task<int> Handle(TaskCreateCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        ValidateAssignment(request);

        var parent = await LoadAndValidateParentAsync(request, ct);

        // Lập kế hoạch: Planning Manager của xưởng / GĐ.
        await ProjectScopeGuard.EnsureCanWriteAsync(request.DepartmentId, _currentUser, _authorizer, ct);

        // Chỉ load project khi có Deadline (để validate scope + khoá ratio). Task không deadline không cần —
        // giữ hợp đồng cũ (task không deadline không phụ thuộc project được load).
        ProjectEntity? project = null;
        if (request.Deadline is not null && request.ProjectId is int pid)
            project = await _projects.GetByIdAsync(pid, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn #{pid}.");

        ValidateDeadlineScope(request, parent, project);
        // Sản lượng scale tự do — bỏ luật "tổng con ≤ sản lượng cha" (% tiến độ do trọng số, không do sản lượng).
        ValidateParentNotDone(parent);

        // Gán người vào vị trí lá: cùng xưởng + đúng chức danh + không InProgress ở task khác (refactor I/P/D).
        if (!request.IsBatchParent)
            await TaskAssigneeGuard.ValidateAsync(
                request.AssigneeUserIds, request.DepartmentId, request.RequiredPositionId,
                Array.Empty<int>(), _departments, _tasks, ct);

        var task = BuildTask(request, userId, project);
        return await _tasks.AddAsync(task, ct);
    }

    // Plan mục 6.3: không cho tạo task con mới trong container đã Done — tránh % tự tụt sau khi đã chốt xong.
    private static void ValidateParentNotDone(ProjectTaskEntity? parent)
    {
        if (parent?.Status == TaskItemStatus.Done)
            throw new DomainValidationException("Không thể thêm task vào node đã hoàn thành (Done).");
    }

    // Container (node gộp) thủ công: không giao ai trực tiếp. Work-leaf Quantity: cần ≥1 người.
    private static void ValidateAssignment(TaskCreateCommand request)
    {
        if (request.IsBatchParent)
        {
            if (request.AssigneeUserIds.Count > 0)
                throw new DomainValidationException("Node gộp (container) không được giao người trực tiếp.");
        }
        else if (request.ProgressType == ProgressType.Quantity && request.AssigneeUserIds.Count == 0
            && request.PlannedHeadcount <= 0)
        {
            // Tạo vị trí lá rỗng (0 assignee) chỉ hợp lệ khi có PlannedHeadcount (gán dần sau).
            throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện hoặc khai báo số người kế hoạch.");
        }
    }

    private async Task<ProjectTaskEntity?> LoadAndValidateParentAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.ParentTaskId is not int parentId) return null;

        var parent = await _tasks.GetByIdAsync(parentId, ct)
            ?? throw new NotFoundException($"Không tìm thấy node cha #{parentId}.");

        if (!parent.IsBatchParent)
            throw new DomainValidationException("Node cha phải là container (node gộp).");
        if (parent.DepartmentId != request.DepartmentId)
            throw new DomainValidationException("Task con phải cùng xưởng với node cha.");

        return parent;
    }

    // Deadline con không được vượt phạm vi cha (container) / đơn. project + parent load sẵn ngoài (tránh N+1).
    private static void ValidateDeadlineScope(
        TaskCreateCommand request, ProjectTaskEntity? parent, ProjectEntity? project)
    {
        if (request.Deadline is not { } childDeadline) return;

        if (parent?.Deadline is { } parentDeadline && childDeadline > parentDeadline)
            throw new DomainValidationException("Deadline task con không được vượt quá deadline node cha.");

        if (project?.Deadline is { } projectDeadline && childDeadline > projectDeadline)
            throw new DomainValidationException("Deadline task không được vượt quá deadline đơn.");
    }

    private static ProjectTaskEntity BuildTask(TaskCreateCommand request, int userId, ProjectEntity? project)
    {
        var now = VietnamClock.Now;
        // Khoá ratio ngay khi tạo để lần đổi timeline đơn sau vẫn giữ deadline task theo ý người dùng
        // (không có project hoặc không đủ timeline → ratio null → task "sticky/manual", cascade bỏ qua).
        var ratio = project is null
            ? null
            : TaskDeadlineCalculator.RatioFromDeadline(project.StartDate, project.Deadline, request.Deadline);
        return new ProjectTaskEntity
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            Type = TaskItemType.AdHoc,
            Status = TaskItemStatus.Todo,
            ProgressType = request.ProgressType,
            IsBatchParent = request.IsBatchParent,
            Priority = request.Priority,
            Deadline = request.Deadline,
            DeadlineOffsetRatio = ratio,
            StepOrder = request.StepOrder,
            // Gate (Milestone) không có sản lượng — PlannedQuantity/CompletedQuantity luôn 0.
            PlannedQuantity = request.ProgressType == ProgressType.Quantity ? request.PlannedQuantity : 0,
            PlannedWorkUnits = request.PlannedWorkUnits,
            PlannedHeadcount = request.IsBatchParent ? 0 : request.PlannedHeadcount,
            RequiredPositionId = request.IsBatchParent ? null : request.RequiredPositionId,
            // IsRequiredGate chỉ có ý nghĩa cho Gate (Milestone).
            IsRequiredGate = request.ProgressType == ProgressType.Milestone && request.IsRequiredGate,
            ProjectId = request.ProjectId,
            DepartmentId = request.DepartmentId,
            ParentTaskId = request.ParentTaskId,
            CreatedByUserId = userId,
            Assignees = request.AssigneeUserIds.Distinct()
                .Select(id => new TaskAssignee { UserId = id, AssignedAt = now })
                .ToList(),
        };
    }

    private static string? Normalize(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
