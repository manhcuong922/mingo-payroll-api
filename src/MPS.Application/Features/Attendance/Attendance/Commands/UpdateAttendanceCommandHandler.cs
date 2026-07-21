using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

/// <summary>
/// Edit lock is resolved from AttendanceSetting.
/// Multi-shift: key bao gồm ShiftTemplateId — sửa đúng record của ca cụ thể.
/// Status nullable — null → resolver suy từ giờ chấm + shift + setting.
/// </summary>
public class UpdateAttendanceCommandHandler : IRequestHandler<UpdateAttendanceCommand, bool>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly IAttendanceStatusResolver _statusResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IClock _clock;

    public UpdateAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        IAttendanceSettingRepository settingRepository,
        IAttendanceStatusResolver statusResolver,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IAttendanceEditLockChecker lockChecker,
        IClock clock)
    {
        _repository = repository;
        _settingRepository = settingRepository;
        _statusResolver = statusResolver;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _lockChecker = lockChecker;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdateAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByKeyAsync(
            request.EmployeeInformationId, request.WorkDate, request.ShiftTemplateId, cancellationToken);
        if (existing is null) return false;

        await _lockChecker.EnsureCanModifyAsync(
            request.WorkDate, request.OverrideReason, cancellationToken);

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var checkInAt = request.CheckInAt;
        var checkOutAt = request.CheckOutAt;
        if (!AttendanceTimeRules.HasValidObservedRange(checkInAt, checkOutAt))
            throw new DomainValidationException("CheckOutAt must be later than CheckInAt.");

        // Resolve status: nếu HR truyền cụ thể → giữ nguyên; nếu null → resolver suy từ shift + setting.
        var resolvedStatus = request.Status;
        if (!resolvedStatus.HasValue && existing.ShiftTemplate is not null)
        {
            var setting = await _settingRepository.GetOrCreateDefaultAsync(_clock.UtcNow, cancellationToken);
            resolvedStatus = _statusResolver.Resolve(
                checkInAt, checkOutAt, existing.ShiftTemplate, existing.WorkDate, setting);
        }
        // Nếu vẫn null (record không có ShiftTemplate — OT), giữ status hiện tại.
        var finalStatus = resolvedStatus ?? existing.Status;

        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter,
            finalStatus,
            checkInAt,
            checkOutAt,
            laborLaw.NightStartTime,
            laborLaw.NightEndTime,
            existing.ShiftTemplate,
            existing.WorkDate);

        existing.CheckInAt = checkInAt;
        existing.CheckOutAt = checkOutAt;
        existing.Status = finalStatus;
        existing.Notes = request.Notes;
        existing.OverrideReason = !string.IsNullOrWhiteSpace(request.OverrideReason)
            ? request.OverrideReason
            : existing.OverrideReason;
        existing.DayHours = dayHours;
        existing.NightHours = nightHours;
        existing.UpdatedAt = _clock.UtcNow;
        existing.UpdatedByUserId = userId;

        return await _repository.UpdateAsync(existing, cancellationToken);
    }
}
