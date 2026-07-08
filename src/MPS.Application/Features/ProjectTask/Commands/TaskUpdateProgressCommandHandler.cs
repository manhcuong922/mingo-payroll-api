using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Common;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskUpdateProgressCommandHandler : IRequestHandler<TaskUpdateProgressCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskUpdateProgressCommandHandler(
        IProjectTaskRepository tasks,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskUpdateProgressCommand request, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteTaskLeafAsync(task, _tasks, _currentUser, _authorizer, ct);

        if (task.ProgressType != ProgressType.Quantity)
            throw new DomainValidationException("Chỉ task có sản lượng (Quantity) mới cập nhật được số lượng.");

        if (task.IsBatchParent)
            throw new DomainValidationException("Không cập nhật sản lượng trực tiếp trên công đoạn (task cha).");

        if (task.Status is TaskItemStatus.Paused or TaskItemStatus.Cancelled)
            throw new DomainValidationException("Task đang bị dừng/hủy, không thể cập nhật.");

        if (request.CompletedQuantity < 0)
            throw new DomainValidationException("Số lượng hoàn thành không được âm.");

        var expectedRowVersion = DecodeRowVersion(request.RowVersion);

        task.CompletedQuantity = Math.Min(request.CompletedQuantity, task.PlannedQuantity);
        task.ProgressUpdatedAt = VietnamClock.Now;
        if (task.Status == TaskItemStatus.Todo && task.CompletedQuantity > 0)
            task.Status = TaskItemStatus.InProgress;

        var ok = await _tasks.UpdateWithConcurrencyAsync(task, expectedRowVersion, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        if (task.ParentTaskId is int parentId)
            await _tasks.RecomputeParentCompletedAsync(parentId, ct);

        return true;
    }

    private static byte[] DecodeRowVersion(string base64)
    {
        try { return Convert.FromBase64String(base64); }
        catch (FormatException) { throw new DomainValidationException("RowVersion không hợp lệ."); }
    }
}
