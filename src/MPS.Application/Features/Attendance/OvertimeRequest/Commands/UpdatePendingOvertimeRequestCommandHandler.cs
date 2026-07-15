using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.OvertimeRequest.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Commands;

/// <summary>
/// NV tự sửa đơn OT của chính mình khi còn Pending (chưa PM review). Guards:
///  - Chỉ CreatedByUserId = current user.
///  - Chỉ status = Pending (không sửa được sau khi PM đã Forward/Reject).
/// Recompute OtHours + day/night split, re-run cap check.
/// </summary>
public class UpdatePendingOvertimeRequestCommandHandler
    : IRequestHandler<UpdatePendingOvertimeRequestCommand, bool>
{
    private readonly IOvertimeRequestRepository _repository;
    private readonly OvertimeCapChecker _capChecker;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdatePendingOvertimeRequestCommandHandler(
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

    public async Task<bool> Handle(UpdatePendingOvertimeRequestCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.CreatedByUserId != userId)
            throw new ForbiddenAccessException("Chỉ người tạo đơn mới được sửa.");

        if (existing.Status != OtRequestStatus.Pending)
            throw new DomainValidationException(
                $"Chỉ sửa được đơn ở trạng thái Chờ duyệt. Trạng thái hiện tại: {existing.Status}.");

        if (request.OtStartTime == request.OtEndTime)
            throw new DomainValidationException("Giờ bắt đầu và kết thúc OT không được trùng.");

        var otHours = CalculateHours(request.OtStartTime, request.OtEndTime);

        // Guard trùng OT: exclude chính đơn đang sửa để không tự conflict với mình.
        var existingSameDay = await _repository.GetActiveOnDateForEmployeeAsync(
            existing.EmployeeInformationId, request.WorkDate, excludeId: existing.Id, cancellationToken);
        var conflict = existingSameDay.FirstOrDefault(e => OvertimeTimeOverlap.Overlaps(
            request.WorkDate, request.OtStartTime, request.OtEndTime,
            e.WorkDate, e.OtStartTime, e.OtEndTime));
        if (conflict is not null)
        {
            throw new DomainValidationException(
                $"NV đã có đơn OT #{conflict.Id} ({conflict.OtStartTime:HH\\:mm}–{conflict.OtEndTime:HH\\:mm}, {conflict.Status}) giao thoa khung giờ mới.");
        }

        // Cap check tính cumulative Pending + PendingApprover + Approved; exclude chính đơn
        // đang sửa để tránh double-count giờ cũ.
        await _capChecker.CheckAsync(
            existing.EmployeeInformationId, request.WorkDate,
            request.OtStartTime, request.OtEndTime, otHours, cancellationToken,
            excludeRequestId: existing.Id);

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var breakdown = _splitter.Split(new WorkHourSplitInput(
            CheckIn: request.OtStartTime,
            CheckOut: request.OtEndTime,
            BreakStart: null,
            BreakEnd: null,
            NightStart: laborLaw.NightStartTime,
            NightEnd: laborLaw.NightEndTime));

        var now = _clock.UtcNow;
        existing.WorkDate = request.WorkDate;
        existing.OtStartTime = request.OtStartTime;
        existing.OtEndTime = request.OtEndTime;
        existing.OtHours = otHours;
        existing.OtDayHours = breakdown.DayHours;
        existing.OtNightHours = breakdown.NightHours;
        // WorkDate có thể đổi — refresh snapshot LaborLaw version cho đúng ngày mới
        existing.LaborLawConstantsVersionId = laborLaw.VersionId;
        existing.Reason = request.Reason;
        // ApproverRoute do PM chọn khi Forward — NV không được đổi.
        existing.UpdatedAt = now;
        existing.UpdatedByUserId = userId;

        await _repository.UpdateAsync(existing, cancellationToken);
        return true;
    }

    private static decimal CalculateHours(TimeOnly start, TimeOnly end)
    {
        var spanMinutes = end > start
            ? (end - start).TotalMinutes
            : (24 * 60 - (start - TimeOnly.MinValue).TotalMinutes) + (end - TimeOnly.MinValue).TotalMinutes;
        return Math.Round((decimal)spanMinutes / 60m, 2);
    }
}
