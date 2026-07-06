using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// BR-10: Self-approval is forbidden.
/// Re-check BR-7/8 tại thời điểm approve (race condition giữa multiple Pending).
/// Multi-shift: KHÔNG auto-upsert AttendanceRecord — vì grain mới (emp, date, shift)
/// có thể có nhiều ca/ngày, OT không chỉ rõ ca nào để gắn. DM/HR chấm công riêng.
/// </summary>
public class ApproveOvertimeRequestCommandHandler : IRequestHandler<ApproveOvertimeRequestCommand, bool>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ApproveOvertimeRequestCommandHandler(
        IOvertimeRequestRepository repository,
        OvertimeCapChecker capChecker,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _capChecker = capChecker;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(ApproveOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.Status != OtRequestStatus.Pending)
            throw new DomainValidationException(
                $"Overtime request {request.Id} cannot be approved — current status is {existing.Status}.");

        // BR-10 self-approval check
        if (existing.CreatedByUserId == userId)
            throw new DomainValidationException("BR-10: Self-approval is forbidden.");

        // Re-check caps for race-condition safety
        await _capChecker.CheckAsync(
            existing.EmployeeInformationId,
            existing.WorkDate,
            existing.OtStartTime,
            existing.OtEndTime,
            existing.OtHours,
            cancellationToken);

        // Phase B: recompute night split tại thời điểm approve — Compliance version
        // có thể đã đổi (vd HR sửa night window) giữa create và approve.
        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(existing.WorkDate, cancellationToken);
        var breakdown = _splitter.Split(new WorkHourSplitInput(
            CheckIn: existing.OtStartTime,
            CheckOut: existing.OtEndTime,
            BreakStart: null,
            BreakEnd: null,
            NightStart: laborLaw.NightStartTime,
            NightEnd: laborLaw.NightEndTime));

        var now = _clock.UtcNow;
        existing.Status = OtRequestStatus.Approved;
        existing.OtDayHours = breakdown.DayHours;
        existing.OtNightHours = breakdown.NightHours;
        existing.ReviewedByUserId = userId;
        existing.ReviewedAt = now;
        existing.ReviewNote = request.ReviewNote;
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        await _repository.UpdateAsync(existing, cancellationToken);
        return true;
    }
}
