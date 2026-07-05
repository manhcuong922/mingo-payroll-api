using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Project;
using MPS.Application.Services;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ProjectTask.Commands;

public class TaskCompleteCommandHandler : IRequestHandler<TaskCompleteCommand, bool>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProductionAttachmentRepository _attachments;
    private readonly IDepartmentManagerAuthorizer _authorizer;
    private readonly ICurrentUserService _currentUser;

    public TaskCompleteCommandHandler(
        IProjectTaskRepository tasks,
        IProductionAttachmentRepository attachments,
        IDepartmentManagerAuthorizer authorizer,
        ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _attachments = attachments;
        _authorizer = authorizer;
        _currentUser = currentUser;
    }

    public async Task<bool> Handle(TaskCompleteCommand request, CancellationToken ct)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Không xác định được người dùng hiện tại.");

        var task = await _tasks.GetByIdAsync(request.Id, ct)
            ?? throw new NotFoundException($"Không tìm thấy task #{request.Id}.");

        await ProjectScopeGuard.EnsureCanWriteTaskAsync(task.DepartmentId, _currentUser, _authorizer, ct);

        if (task.IsBatchParent)
            throw new DomainValidationException("Không hoàn thành trực tiếp công đoạn (task cha) — cha tự Done khi mọi con Done.");

        if (task.Status == TaskItemStatus.Done)
            return true;

        // Task bàn giao: bắt buộc có ít nhất 1 bằng chứng nghiệm thu (TaskCompletion).
        if (task.RequiresHandover)
        {
            var count = await _attachments.CountByOwnerAsync(AttachmentOwnerType.TaskCompletion, task.Id, ct);
            if (count == 0)
                throw new DomainValidationException("Task bàn giao phải đính kèm bằng chứng nghiệm thu trước khi hoàn thành.");
        }

        var now = DateTime.UtcNow;
        var expectedRowVersion = DecodeRowVersion(request.RowVersion);

        task.Status = TaskItemStatus.Done;
        task.CompletedByUserId = userId;
        task.CompletedAt = now;
        task.WasLate = task.Deadline is { } dl && now > dl;

        var ok = await _tasks.UpdateWithConcurrencyAsync(task, expectedRowVersion, ct);
        if (!ok)
            throw new ConflictException("Dữ liệu đã thay đổi ở nơi khác. Vui lòng tải lại rồi thử lại.");

        // Cha tự Done nguyên tử khi con cuối cùng vừa Done.
        if (task.ParentTaskId is int parentId)
            await _tasks.TrySetParentDoneAsync(parentId, now, userId, ct);

        return true;
    }

    private static byte[] DecodeRowVersion(string base64)
    {
        try { return Convert.FromBase64String(base64); }
        catch (FormatException) { throw new DomainValidationException("RowVersion không hợp lệ."); }
    }
}
