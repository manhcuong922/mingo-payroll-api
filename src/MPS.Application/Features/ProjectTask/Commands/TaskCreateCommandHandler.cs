using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Constants;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using ProjectTaskEntity = MPS.Domain.Entities.ProjectTask;
using TaskAssignee = MPS.Domain.Entities.TaskAssignee;

namespace MPS.Application.Features.ProjectTask.Commands;

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

        if (!request.IsBatchParent && request.ProgressType == ProgressType.Milestone && parent is null)
            await ProjectScopeGuard.EnsureCanWriteAsync(request.DepartmentId, _currentUser, _authorizer, ct);
        else
            await ProjectScopeGuard.EnsureCanCreateTaskAsync(
                request.IsBatchParent, request.DepartmentId, parent?.AssignedPmUserId, _currentUser, _authorizer, ct);

        await ValidateDeadlineScopeAsync(request, parent, ct);
        await ValidateQuantityScopeAsync(request, parent, ct);
        await ValidateAssigneesInDepartmentAsync(request, ct);
        await ValidateAssignedPmAsync(request, ct);
        ValidateParentNotDone(parent);
        await ValidateAssigneesNotAlreadyBusyAsync(request, ct);

        var task = BuildTask(request, userId);
        return await _tasks.AddAsync(task, ct);
    }

    // Plan mục 3.2: AssignedPmUserId (nếu có, chỉ khi IsBatchParent) phải role ProjectManager, cùng xưởng Đơn.
    private async Task ValidateAssignedPmAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.AssignedPmUserId is not int pmUserId) return;

        if (!request.IsBatchParent)
            throw new DomainValidationException("Chỉ Công đoạn (IsBatchParent=true) mới gán được PM chịu trách nhiệm.");

        var isValidPm = await _departments.IsUserInDepartmentWithRoleAsync(
            pmUserId, request.DepartmentId, SystemRoles.ProjectManagerId, ct);
        if (!isValidPm)
            throw new DomainValidationException("PM được gán phải có role ProjectManager và cùng xưởng với đơn.");
    }

    // Plan mục 6.3: không cho tạo task con mới trong Công đoạn đã Done — tránh % tự tụt sau khi đã chốt xong.
    private static void ValidateParentNotDone(ProjectTaskEntity? parent)
    {
        if (parent?.Status == TaskItemStatus.Done)
            throw new DomainValidationException("Không thể thêm task vào công đoạn đã hoàn thành (Done).");
    }

    // Công đoạn (tầng 2) thủ công: không giao ai trực tiếp, không có cha. Task lá: cần ≥1 người.
    private static void ValidateAssignment(TaskCreateCommand request)
    {
        if (request.IsBatchParent)
        {
            if (request.AssigneeUserIds.Count > 0)
                throw new DomainValidationException("Công đoạn (task cha) không được giao người trực tiếp.");
            if (request.ParentTaskId is not null)
                throw new DomainValidationException("Công đoạn (task cha) không thể có cha khác — cây chỉ 3 tầng.");
        }
        else if (request.ProgressType == ProgressType.Quantity && request.AssigneeUserIds.Count == 0)
        {
            throw new DomainValidationException("Task phải có ít nhất 1 người thực hiện.");
        }
    }

    private async Task<ProjectTaskEntity?> LoadAndValidateParentAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.ParentTaskId is not int parentId) return null;

        var parent = await _tasks.GetByIdAsync(parentId, ct)
            ?? throw new NotFoundException($"Không tìm thấy công đoạn cha #{parentId}.");

        if (!parent.IsBatchParent)
            throw new DomainValidationException("Task cha phải là công đoạn (IsBatchParent=true).");
        if (parent.DepartmentId != request.DepartmentId)
            throw new DomainValidationException("Task con phải cùng xưởng với công đoạn cha.");

        return parent;
    }

    // Deadline con không được vượt phạm vi cha (công đoạn) / đơn.
    private async Task ValidateDeadlineScopeAsync(TaskCreateCommand request, ProjectTaskEntity? parent, CancellationToken ct)
    {
        if (request.Deadline is not { } childDeadline) return;

        if (parent?.Deadline is { } parentDeadline && childDeadline > parentDeadline)
            throw new DomainValidationException("Deadline task con không được vượt quá deadline công đoạn cha.");

        if (request.ProjectId is not int projectId) return;

        var project = await _projects.GetByIdAsync(projectId, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{projectId}.");
        if (project.Deadline is { } projectDeadline && childDeadline > projectDeadline)
            throw new DomainValidationException("Deadline task không được vượt quá deadline đơn.");
    }

    // Tổng sản lượng các task con Quantity không được vượt sản lượng công đoạn cha. Gate (Milestone)
    // không có sản lượng nên không tham gia — có thể tạo tự do dưới công đoạn miễn còn dư ngân sách con Quantity.
    private async Task ValidateQuantityScopeAsync(TaskCreateCommand request, ProjectTaskEntity? parent, CancellationToken ct)
    {
        if (request.IsBatchParent || parent is null || parent.ProgressType != ProgressType.Quantity
            || request.ProgressType != ProgressType.Quantity) return;

        var siblingsPlanned = await _tasks.SumChildrenPlannedQuantityAsync(parent.Id, excludeTaskId: null, ct);
        if (siblingsPlanned + request.PlannedQuantity > parent.PlannedQuantity)
            throw new DomainValidationException(
                $"Tổng sản lượng các task con ({siblingsPlanned + request.PlannedQuantity}) vượt quá sản lượng công đoạn cha ({parent.PlannedQuantity}).");
    }

    // Mọi assignee phải là công nhân thuộc đúng xưởng.
    private async Task ValidateAssigneesInDepartmentAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.AssigneeUserIds.Count == 0) return;

        var members = (await _departments.GetMemberUserIdsAsync(request.DepartmentId, ct)).ToHashSet();
        var outsiders = request.AssigneeUserIds.Where(id => !members.Contains(id)).ToList();
        if (outsiders.Count > 0)
            throw new DomainValidationException("Có người được giao không thuộc xưởng của task.");
    }

    // Plan mục 4 ràng buộc 4 / mục 9 điểm 2: 1 nhân viên chỉ 1 task Tầng 3 đang mở (chưa Done) tại 1 thời điểm —
    // áp cho cả vai "chịu trách nhiệm" lẫn "thành viên hỗ trợ" (không phân biệt vai trong Assignees).
    private async Task ValidateAssigneesNotAlreadyBusyAsync(TaskCreateCommand request, CancellationToken ct)
    {
        if (request.IsBatchParent || request.AssigneeUserIds.Count == 0) return;

        foreach (var userId in request.AssigneeUserIds.Distinct())
        {
            var openCount = await _tasks.CountActiveByUserAsync(userId, ct);
            if (openCount > 0)
                throw new DomainValidationException(
                    $"Nhân viên #{userId} đang có task chưa hoàn thành, không thể giao thêm task mới.");
        }
    }

    private static ProjectTaskEntity BuildTask(TaskCreateCommand request, int userId)
    {
        var now = VietnamClock.Now;
        return new ProjectTaskEntity
        {
            Title = request.Title.Trim(),
            Description = Normalize(request.Description),
            Type = TaskItemType.AdHoc,
            Status = TaskItemStatus.Todo,
            ProgressType = request.ProgressType,
            IsBatchParent = request.IsBatchParent,
            AssignedPmUserId = request.IsBatchParent ? request.AssignedPmUserId : null,
            Priority = request.Priority,
            Deadline = request.Deadline,
            StepOrder = request.StepOrder,
            // Gate (Milestone) không có sản lượng — PlannedQuantity/CompletedQuantity luôn 0.
            PlannedQuantity = request.ProgressType == ProgressType.Quantity ? request.PlannedQuantity : 0,
            // PlannedWorkUnits chỉ có ý nghĩa cho công đoạn (tỷ trọng lên đơn).
            PlannedWorkUnits = request.IsBatchParent ? request.PlannedWorkUnits : 0,
            // IsRequiredGate chỉ có ý nghĩa cho Gate (Milestone) — task Quantity mặc định entity (true) vô nghĩa, không đọc.
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
