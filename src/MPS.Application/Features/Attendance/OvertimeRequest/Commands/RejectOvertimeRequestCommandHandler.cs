using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// Approver (HR hoặc DM tuỳ ApproverRoute) reject cuối OT — luồng v3 §5.4.
///
/// Guard giống <see cref="ApproveOvertimeRequestCommandHandler"/>: yêu cầu
/// Status=PendingApprover + route match + not self + (DM only) same-department.
/// Ghi ApproverReview* + mirror ReviewedBy*, set Status=Rejected.
/// </summary>
public class RejectOvertimeRequestCommandHandler : IRequestHandler<RejectOvertimeRequestCommand, bool>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly IProjectRepository _projectRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RejectOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        IProjectRepository projectRepository,
        IEmployeeInformationRepository employeeRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _projectRepository = projectRepository;
        _employeeRepository = employeeRepository;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(RejectOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != OtRequestStatus.PendingApprover)
            throw new DomainValidationException(
                $"Overtime request {request.Id} cannot be rejected — current status is {existing.Status}. Cần PM Forward trước.");

        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: bạn không thể reject request của chính mình.");
        if (existing.PmReviewedByUserId == userId)
            throw new DomainValidationException("BR-10: bạn đã forward request này với vai trò PM — không được reject cuối.");

        ApproveOvertimeRequestCommandHandler.EnsureApproverRouteMatches(existing.ApproverRoute, _currentUser.RoleId);

        // DM route: DM phải cùng dept với Project
        if (existing.ApproverRoute == OvertimeApproverRoute.DepartmentManager
            && existing.ProjectId is int projectId)
        {
            var project = await _projectRepository.GetByIdAsync(projectId, cancellationToken)
                ?? throw new NotFoundException($"Project {projectId} not found.");
            if (project.DepartmentId is not int projectDeptId)
                throw new DomainValidationException("Project không gắn phòng ban — không xác định được DM reject.");

            var dmEmployeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
                ?? throw new NotFoundException($"No employee profile linked to user {userId}.");
            var dm = await _employeeRepository.GetByIdAsync(dmEmployeeId, cancellationToken)
                ?? throw new NotFoundException($"Employee {dmEmployeeId} not found.");
            if (dm.DepartmentId != projectDeptId)
                throw new ForbiddenAccessException("Bạn không phải quản lý phòng ban của đơn này — không được reject.");
        }

        var now = _clock.UtcNow;
        existing.Status = OtRequestStatus.Rejected;

        // v3: Audit tầng 2 — Approver decide
        existing.ApproverReviewedByUserId = userId;
        existing.ApproverReviewedAt = now;
        existing.ApproverReviewNote = request.ReviewNote;

        // Mirror final reviewer
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;

        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
