using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskCompleteCommandHandler : IRequestHandler<TaskCompleteCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskCompleteCommandHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskCompleteCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanUpdateProgressAsync(task, _projects, _currentUser, _authorizer, ct);

        if (task.IsBatchParent)
            throw new DomainValidationException("Không hoàn thành trực tiếp node gộp (container) — cha tự Done khi mọi con Done.");

        if (task.Status == TaskItemStatus.Done)
            return true;

        if (task.Status is TaskItemStatus.Paused or TaskItemStatus.Cancelled)
            throw new DomainValidationException("Task đang bị dừng/hủy, không thể cập nhật.");

        // Tài liệu nghiệm thu (TaskCompletion) là tuỳ chọn — đính kèm như tài liệu thường, không chặn hoàn thành.

        var now = VietnamClock.Now;
        var completedAt = request.CompletedAt ?? now;
        var expectedRowVersion = DecodeRowVersion(request.RowVersion);

        task.Status = TaskItemStatus.Done;
        task.CompletedByUserId = userId;
        task.CompletedAt = completedAt;
        task.WasLate = task.Deadline is { } dl && completedAt > dl;
        if (task.ProgressType == ProgressType.Quantity)
        {
            task.CompletedQuantity = task.PlannedQuantity;
            task.ProgressUpdatedAt = now;
        }
        // Gate (Milestone) hoàn thành chỉ đổi Status/CompletedAt/CompletedByUserId — không có sản lượng
        // để set (docs/plans/plan.md mục 4.2), CompletedQuantity giữ nguyên 0.

        var ok = await _tasks.UpdateWithConcurrencyAsync(task, expectedRowVersion, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        // Rollup đệ quy lên tổ tiên: mỗi container tự cộng sản lượng + tự Done khi đủ điều kiện (refactor).
        if (task.ParentTaskId is not null)
            await _tasks.RollupAncestorsAsync(task.Id, completedAt, userId, ct);

        return true;
    }

    private static byte[] DecodeRowVersion(string base64)
    {
        try { return Convert.FromBase64String(base64); }
        catch (FormatException) { throw new DomainValidationException("RowVersion không hợp lệ."); }
    }
}
