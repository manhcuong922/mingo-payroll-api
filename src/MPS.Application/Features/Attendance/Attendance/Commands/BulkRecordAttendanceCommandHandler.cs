using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Domain.Constants;
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
    private const int EditWindowDays = 3;

    private readonly IAttendanceRecordRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public BulkRecordAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        IShiftTemplateRepository shiftRepository,
        IContractRepository contractRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _contractRepository = contractRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<BulkRecordAttendanceResult> Handle(BulkRecordAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var hasOverride = _currentUser.HasPermission(Permissions.AttendanceAdjustmentOverrideAll);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var daysSince = today.DayNumber - request.WorkDate.DayNumber;

        var created = 0;
        var updated = 0;
        var failed = 0;
        var items = new List<BulkRecordAttendanceItemResult>(request.Records.Count);

        foreach (var row in request.Records)
        {
            try
            {
                var futureError = AttendanceTimeRules.ValidateNotInFuture(
                    request.WorkDate, row.CheckInTime, row.CheckOutTime, _clock.UtcNow);
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

                if (existing is null)
                {
                    if (!await _contractRepository.HasActiveContractAsync(row.EmployeeInformationId, cancellationToken))
                        throw new DomainValidationException(
                            $"Employee {row.EmployeeInformationId} has no active contract; cannot record attendance.");

                    var entity = new AttendanceRecordEntity
                    {
                        EmployeeInformationId = row.EmployeeInformationId,
                        WorkDate = request.WorkDate,
                        ShiftTemplateId = row.ShiftTemplateId,
                        CheckInTime = row.CheckInTime,
                        CheckOutTime = row.CheckOutTime,
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
                    if (!hasOverride && daysSince > EditWindowDays)
                        throw new DomainValidationException(
                            $"BR-2: Cannot edit attendance for {request.WorkDate:yyyy-MM-dd} — outside the {EditWindowDays}-day edit window.");

                    if (hasOverride && daysSince > EditWindowDays && string.IsNullOrWhiteSpace(row.OverrideReason))
                        throw new DomainValidationException(
                            "BR-3: OverrideReason is required when HR edits an attendance record outside the 3-day edit window.");

                    existing.CheckInTime = row.CheckInTime;
                    existing.CheckOutTime = row.CheckOutTime;
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
