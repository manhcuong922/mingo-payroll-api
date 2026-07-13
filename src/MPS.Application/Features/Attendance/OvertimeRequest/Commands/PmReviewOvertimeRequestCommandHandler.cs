using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Handler cho <see cref="PmReviewOvertimeRequestCommand"/> — luồng OT v3 §5.3.
///
/// Flow:
///  1. Load request → NotFound.
///  2. Guard Status = Pending (PendingPm). Nếu đã xử → throw (chống race giữa nhiều PM).
///  3. Load Project → gate currentUser.Id == Project.ResponsibleUserId.
///  4. Self-review guard: currentUser != request.CreatedByUserId.
///  5. Forward:
///        - Set ApproverRoute (Hr | DepartmentManager)
///        - Set PmReview* + Status = PendingApprover
///     Reject:
///        - Set PmReview* + mirror ReviewedBy* + Status = Rejected
///        - ApproverRoute stays null
///  6. SaveChanges.
/// </summary>
public class PmReviewOvertimeRequestCommandHandler : IRequestHandler<PmReviewOvertimeRequestCommand, bool>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public PmReviewOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        IProjectRepository projectRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(PmReviewOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.RequestId, cancellationToken);
        if (existing is null) return false;

        // Guard status — chống race giữa nhiều PM cùng inbox
        if (existing.Status != OtRequestStatus.Pending)
            throw new DomainValidationException(
                $"Request #{existing.Id} đã ở trạng thái {existing.Status}, PM không thể review lại.");

        if (existing.ProjectId is not int projectId)
            throw new DomainValidationException(
                $"Request #{existing.Id} không gắn với đơn sản xuất — không xác định được tổ trưởng để review.");

        var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
            ?? throw new NotFoundException($"Project {projectId} not found.");

        if (project.ResponsibleUserId != userId)
            throw new ForbiddenAccessException(
                "Bạn không phải tổ trưởng đơn này — không có quyền review request OT.");

        // Self-review: PM không được review request mình tự đăng ký
        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException(
                "BR-10: bạn không thể tự review request của chính mình.");

        var now = _clock.UtcNow;

        // Audit tầng 1 — luôn ghi PmReview*
        existing.PmReviewedByUserId = userId;
        existing.PmReviewedAt = now;
        existing.PmReviewNote = request.Note;

        switch (request.Decision)
        {
            case PmReviewDecision.Forward:
                if (request.ApproverRoute is not OvertimeApproverRoute route)
                    throw new DomainValidationException(
                        "Forward yêu cầu chọn route duyệt (HR hoặc DM).");
                existing.ApproverRoute = route;
                existing.Status = OtRequestStatus.PendingApprover;
                // KHÔNG set ReviewedBy* — chưa final
                break;

            case PmReviewDecision.Reject:
                if (request.ApproverRoute is not null)
                    throw new DomainValidationException(
                        "Reject không được đi kèm route duyệt.");
                existing.Status = OtRequestStatus.Rejected;
                // Mirror final reviewer — payroll & query cũ đọc ReviewedBy*/ReviewNote
                existing.ReviewedByUserId = userId;
                existing.ReviewedAt = now;
                existing.ReviewNote = request.Note;
                break;

            default:
                throw new DomainValidationException($"Unknown PM decision: {request.Decision}.");
        }

        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        await _repository.UpdateAsync(existing, cancellationToken);
        return true;
    }
}
