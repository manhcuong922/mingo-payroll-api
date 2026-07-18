using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

/// <summary>
/// FT-11 bulk upsert — roster ngày: ghi nhận chấm công nhiều NV cùng lúc.
/// Mỗi row độc lập theo grain (emp, date, shift). Multi-shift: cùng 1 NV có thể
/// xuất hiện trong batch nhiều lần với ShiftTemplateId khác nhau.
/// </summary>
public class BulkRecordAttendanceCommandHandler : IRequestHandler<BulkRecordAttendanceCommand, BulkRecordAttendanceResult>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IClock _clock;

    public BulkRecordAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        IShiftTemplateRepository shiftRepository,
        IEmployeeInformationRepository employeeRepository,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IAttendanceEditLockChecker lockChecker,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _employeeRepository = employeeRepository;
        _currentUser = currentUser;
        _splitter = splitter;
        _complianceLookup = complianceLookup;
        _lockChecker = lockChecker;
        _clock = clock;
    }

    public async Task<BulkRecordAttendanceResult> Handle(BulkRecordAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);

        var created = 0;
        var updated = 0;
        var failed = 0;
        var items = new List<BulkRecordAttendanceItemResult>(request.Records.Count);

        foreach (var row in request.Records)
        {
            try
            {
                await _lockChecker.EnsureCanModifyAsync(
                    request.WorkDate, row.OverrideReason, cancellationToken);

                var rowCheckInAt = row.CheckInAt;
                var rowCheckOutAt = row.CheckOutAt;
                if (!AttendanceTimeRules.HasValidObservedRange(rowCheckInAt, rowCheckOutAt))
                    throw new DomainValidationException("CheckOutAt must be later than CheckInAt.");

                var futureError = AttendanceTimeRules.ValidateNotInFuture(
                    request.WorkDate, rowCheckInAt, rowCheckOutAt, _clock.UtcNow);
                if (futureError is not null)
                    throw new DomainValidationException(futureError);

                var shift = await _shiftRepository.GetByIdAsync(row.ShiftTemplateId, cancellationToken)
                    ?? throw new DomainValidationException(
                        $"ShiftTemplate {row.ShiftTemplateId} not found.");
                if (!shift.IsActive)
                    throw new DomainValidationException(
                        $"ShiftTemplate '{shift.Code}' is inactive.");

                var existing = await _repository.GetByKeyAsync(
                    row.EmployeeInformationId, request.WorkDate, row.ShiftTemplateId, cancellationToken);

                var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
                    _splitter,
                    row.Status,
                    rowCheckInAt,
                    rowCheckOutAt,
                    laborLaw.NightStartTime,
                    laborLaw.NightEndTime,
                    shift,
                    request.WorkDate);

                if (existing is null)
                {
                    if (!await _employeeRepository.HasActiveUserAsync(row.EmployeeInformationId, cancellationToken))
                        throw new DomainValidationException(
                            $"Employee {row.EmployeeInformationId} không có tài khoản active; không thể chấm công.");

                    var entity = new AttendanceRecordEntity
                    {
                        EmployeeInformationId = row.EmployeeInformationId,
                        WorkDate = request.WorkDate,
                        ShiftTemplateId = row.ShiftTemplateId,
                        CheckInAt = rowCheckInAt,
                        CheckOutAt = rowCheckOutAt,
                        DayHours = dayHours,
                        NightHours = nightHours,
                        Status = row.Status,
                        Notes = row.Notes,
                        CreatedAt = _clock.UtcNow,
                        CreatedByUserId = userId
                    };
                    await _repository.AddAsync(entity, cancellationToken);
                    created++;
                    items.Add(new BulkRecordAttendanceItemResult(row.EmployeeInformationId, row.ShiftTemplateId, true, "Created", null));
                }
                else
                {
                    existing.CheckInAt = rowCheckInAt;
                    existing.CheckOutAt = rowCheckOutAt;
                    existing.DayHours = dayHours;
                    existing.NightHours = nightHours;
                    existing.Status = row.Status;
                    existing.Notes = row.Notes;
                    if (!string.IsNullOrWhiteSpace(row.OverrideReason))
                        existing.OverrideReason = row.OverrideReason;
                    existing.UpdatedAt = _clock.UtcNow;
                    existing.UpdatedByUserId = userId;
                    await _repository.UpdateAsync(existing, cancellationToken);
                    updated++;
                    items.Add(new BulkRecordAttendanceItemResult(row.EmployeeInformationId, row.ShiftTemplateId, true, "Updated", null));
                }
            }
            catch (DomainValidationException ex)
            {
                failed++;
                items.Add(new BulkRecordAttendanceItemResult(row.EmployeeInformationId, row.ShiftTemplateId, false, "Failed", ex.Message));
            }
            catch (ConflictException ex)
            {
                failed++;
                items.Add(new BulkRecordAttendanceItemResult(row.EmployeeInformationId, row.ShiftTemplateId, false, "Failed", ex.Message));
            }
        }

        return new BulkRecordAttendanceResult(created, updated, failed, items);
    }
}
