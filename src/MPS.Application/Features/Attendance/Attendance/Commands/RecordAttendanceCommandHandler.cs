using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Services;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

/// <summary>
/// 2 mode chấm công:
///  • ShiftMode: shift assignment thông thường, ShiftTemplateId set
///  • OvertimeMode: OT đã Approved, OvertimeRequestId set, ShiftTemplateId null
/// Handler enforce exactly-one-of, validate active, chống chấm trùng.
/// Status nullable — nếu null:
///  • Shift record → resolver suy từ giờ chấm + shift + setting.
///  • OT record → default Present khi có check-in, Absent nếu không.
/// </summary>
public class RecordAttendanceCommandHandler : IRequestHandler<RecordAttendanceCommand, int>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IOvertimeRequestRepository _overtimeRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IAttendanceSettingRepository _settingRepository;
    private readonly IAttendanceStatusResolver _statusResolver;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IClock _clock;

    public RecordAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        IShiftTemplateRepository shiftRepository,
        IOvertimeRequestRepository overtimeRepository,
        IEmployeeInformationRepository employeeRepository,
        IAttendanceSettingRepository settingRepository,
        IAttendanceStatusResolver statusResolver,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IAttendanceEditLockChecker lockChecker,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _overtimeRepository = overtimeRepository;
        _employeeRepository = employeeRepository;
        _settingRepository = settingRepository;
        _statusResolver = statusResolver;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _lockChecker = lockChecker;
        _clock = clock;
    }

    public async Task<int> Handle(RecordAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        // Exactly-one-of: ShiftTemplateId XOR OvertimeRequestId
        var hasShift = request.ShiftTemplateId is not null;
        var hasOt = request.OvertimeRequestId is not null;
        if (hasShift == hasOt)
            throw new DomainValidationException(
                "Phải cung cấp CHÍNH XÁC 1 trong 2: ShiftTemplateId (chấm ca chính) hoặc OvertimeRequestId (chấm OT).");

        await _lockChecker.EnsureCanModifyAsync(
            request.WorkDate, request.OverrideReason, cancellationToken);

        if (!await _employeeRepository.HasActiveUserAsync(request.EmployeeInformationId, cancellationToken))
            throw new DomainValidationException(
                $"Employee {request.EmployeeInformationId} không có tài khoản active; không thể chấm công.");

        var checkInAt = request.CheckInAt;
        var checkOutAt = request.CheckOutAt;
        if (!AttendanceTimeRules.HasValidObservedRange(checkInAt, checkOutAt))
            throw new DomainValidationException("CheckOutAt must be later than CheckInAt.");

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);

        return hasShift
            ? await CreateShiftRecordAsync(request, userId, checkInAt, checkOutAt, laborLaw, cancellationToken)
            : await CreateOvertimeRecordAsync(request, userId, checkInAt, checkOutAt, laborLaw, cancellationToken);
    }

    private async Task<int> CreateShiftRecordAsync(
        RecordAttendanceCommand request,
        int userId,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot laborLaw,
        CancellationToken ct)
    {
        var shiftId = request.ShiftTemplateId!.Value;
        var shift = await _shiftRepository.GetByIdAsync(shiftId, ct)
            ?? throw new DomainValidationException($"ShiftTemplate {shiftId} not found or inactive.");
        if (!shift.IsActive)
            throw new DomainValidationException($"ShiftTemplate '{shift.Code}' is inactive; cannot record attendance.");

        var existing = await _repository.GetByKeyAsync(
            request.EmployeeInformationId, request.WorkDate, shiftId, ct);
        if (existing is not null)
            throw new ConflictException(
                $"Attendance record already exists for employee {request.EmployeeInformationId} on {request.WorkDate:yyyy-MM-dd} shift {shift.Code}.");

        var resolvedStatus = await ResolveShiftStatusAsync(
            request.Status, checkInAt, checkOutAt, shift, request.WorkDate, ct);

        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, resolvedStatus, checkInAt, checkOutAt,
            laborLaw.NightStartTime, laborLaw.NightEndTime, shift, request.WorkDate);

        var entity = BuildEntity(request, resolvedStatus, userId, checkInAt, checkOutAt, dayHours, nightHours);
        entity.ShiftTemplateId = shiftId;
        entity.OvertimeRequestId = null;
        return await _repository.AddAsync(entity, ct);
    }

    private async Task<int> CreateOvertimeRecordAsync(
        RecordAttendanceCommand request,
        int userId,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        MPS.Application.Common.Models.Compliance.LaborLawConstantsSnapshot laborLaw,
        CancellationToken ct)
    {
        var otId = request.OvertimeRequestId!.Value;
        var ot = await _overtimeRepository.GetByIdAsync(otId, ct)
            ?? throw new DomainValidationException($"OvertimeRequest {otId} not found.");

        if (ot.EmployeeInformationId != request.EmployeeInformationId)
            throw new DomainValidationException(
                $"OvertimeRequest {otId} thuộc NV khác (empId={ot.EmployeeInformationId}), không khớp {request.EmployeeInformationId}.");

        if (ot.Status != OtRequestStatus.Approved)
            throw new DomainValidationException(
                $"OvertimeRequest {otId} chưa được duyệt (Status={ot.Status}). Chỉ chấm được OT đã Approved.");

        if (ot.WorkDate != request.WorkDate)
            throw new DomainValidationException(
                $"OvertimeRequest {otId} có WorkDate={ot.WorkDate:yyyy-MM-dd}, không khớp {request.WorkDate:yyyy-MM-dd}.");

        // OvertimeRequestId unique (index filter) → BE sẽ throw nếu chấm trùng.
        // Explicit check trước để trả error message rõ ràng.
        var existing = await _repository.GetByOvertimeRequestIdAsync(otId, ct);
        if (existing is not null)
            throw new ConflictException(
                $"OvertimeRequest {otId} đã được chấm công (record {existing.Id}).");

        // OT record: resolver không áp dụng. Nếu HR truyền status = null → default Present khi có check-in, Absent nếu không.
        var resolvedStatus = request.Status ?? (checkInAt is null ? AttendanceStatus.Absent : AttendanceStatus.Present);

        // Split hours không cần shift (không có break) — pass null cho break.
        var (dayHours, nightHours) = SplitHoursWithoutShift(
            resolvedStatus, checkInAt, checkOutAt, laborLaw.NightStartTime, laborLaw.NightEndTime);

        var entity = BuildEntity(request, resolvedStatus, userId, checkInAt, checkOutAt, dayHours, nightHours);
        entity.ShiftTemplateId = null;
        entity.OvertimeRequestId = otId;
        return await _repository.AddAsync(entity, ct);
    }

    /// <summary>
    /// Nếu HR truyền status cụ thể → giữ nguyên (override thủ công).
    /// Nếu null → load setting + gọi resolver suy status từ giờ chấm.
    /// </summary>
    private async Task<AttendanceStatus> ResolveShiftStatusAsync(
        AttendanceStatus? explicitStatus,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        MPS.Domain.Entities.Attendance.ShiftTemplate shift,
        DateOnly workDate,
        CancellationToken ct)
    {
        if (explicitStatus.HasValue) return explicitStatus.Value;

        var setting = await _settingRepository.GetOrCreateDefaultAsync(_clock.UtcNow, ct);
        return _statusResolver.Resolve(checkInAt, checkOutAt, shift, workDate, setting);
    }

    private (decimal DayHours, decimal NightHours) SplitHoursWithoutShift(
        AttendanceStatus status,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        TimeOnly nightStart,
        TimeOnly nightEnd)
    {
        if (status is AttendanceStatus.OnLeave or AttendanceStatus.Absent) return (0m, 0m);
        if (checkInAt is null || checkOutAt is null) return (0m, 0m);

        var breakdown = _splitter.Split(new WorkHourDateTimeSplitInput(
            CheckInAt: checkInAt.Value,
            CheckOutAt: checkOutAt.Value,
            BreakStart: null,
            BreakEnd: null,
            NightStart: nightStart,
            NightEnd: nightEnd));
        return (breakdown.DayHours, breakdown.NightHours);
    }

    private AttendanceRecordEntity BuildEntity(
        RecordAttendanceCommand request,
        AttendanceStatus resolvedStatus,
        int userId,
        DateTime? checkInAt,
        DateTime? checkOutAt,
        decimal dayHours,
        decimal nightHours) =>
        new()
        {
            EmployeeInformationId = request.EmployeeInformationId,
            WorkDate = request.WorkDate,
            CheckInAt = checkInAt,
            CheckOutAt = checkOutAt,
            DayHours = dayHours,
            NightHours = nightHours,
            Status = resolvedStatus,
            Notes = request.Notes,
            OverrideReason = !string.IsNullOrWhiteSpace(request.OverrideReason)
                ? request.OverrideReason.Trim()
                : null,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId,
        };
}
