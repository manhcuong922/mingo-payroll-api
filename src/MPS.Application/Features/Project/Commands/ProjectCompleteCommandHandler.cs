using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Evaluation;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Project.Commands;

public class ProjectCompleteCommandHandler : IRequestHandler<ProjectCompleteCommand, bool>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;
    private readonly ILogger<ProjectCompleteCommandHandler> _log;

    public ProjectCompleteCommandHandler(
        IProjectRepository projects,
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser,
        IPublisher publisher,
        ILogger<ProjectCompleteCommandHandler> log)
    {
        _projects = projects;
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
        _publisher = publisher;
        _log = log;
    }

    public async Task<bool> Handle(ProjectCompleteCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var project = await _projects.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy đơn #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteAsync(project.DepartmentId, _currentUser, _authorizer, ct);

        await EnsureRequiredStagesAndGatesDoneAsync(request.Id, ct);

        project.Status = ProjectStatus.Completed;
        project.CompletedByUserId = userId;
        project.CompletedAt = request.CompletedAt ?? VietnamClock.Now;
        project.CompletionNote = string.IsNullOrWhiteSpace(request.CompletionNote)
            ? null : request.CompletionNote.Trim();

        var ok = await _projects.UpdateAsync(project, ct);

        // Sinh bảng đánh giá theo đơn (D9). Đóng đơn là hành vi CHÍNH; sinh bảng là phụ —
        // bọc try/catch để lỗi sinh bảng KHÔNG làm fail việc đóng đơn (HR có thể xử lý tay sau).
        if (ok)
        {
            try
            {
                await _publisher.Publish(new ProjectCompletedNotification(project.Id), ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Lỗi khi sinh bảng đánh giá cho đơn #{ProjectId} (đơn vẫn đã đóng thành công).", project.Id);
            }
        }

        return ok;
    }

    // docs/plans/plan.md mục 4.4: chỉ cho đóng đơn khi mọi công đoạn (không Paused/Cancelled) đã Done
    // VÀ mọi Gate bắt buộc (IsRequiredGate=true) trong đơn đã Done. Không có ForceComplete trong lần này —
    // mặc định không cho đóng thủ công dù thiếu.
    private async Task EnsureRequiredStagesAndGatesDoneAsync(int projectId, CancellationToken ct)
    {
        var tasks = await _tasks.GetByProjectIdAsync(projectId, ct);

        var pendingStages = tasks
            .Where(t => t.IsBatchParent
                && t.Status is not (TaskItemStatus.Done or TaskItemStatus.Paused or TaskItemStatus.Cancelled))
            .Select(t => t.Title)
            .ToList();

        var pendingGates = tasks
            .Where(t => !t.IsBatchParent && t.ProgressType == ProgressType.Milestone && t.IsRequiredGate
                && t.Status is not (TaskItemStatus.Done or TaskItemStatus.Paused or TaskItemStatus.Cancelled))
            .Select(t => t.Title)
            .ToList();

        if (pendingStages.Count == 0 && pendingGates.Count == 0) return;

        var parts = new List<string>();
        if (pendingStages.Count > 0) parts.Add($"công đoạn chưa hoàn thành: {string.Join(", ", pendingStages)}");
        if (pendingGates.Count > 0) parts.Add($"gate bắt buộc chưa hoàn thành: {string.Join(", ", pendingGates)}");

        throw new DomainValidationException($"Không thể đóng đơn — còn {string.Join("; ", parts)}.");
    }
}
