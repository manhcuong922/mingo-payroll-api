using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using AttendanceAdjustmentEntity = MPS.Domain.Entities.Attendance.AttendanceAdjustment;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

/// <summary>
/// FT-14 HR override path: bypass workflow and directly overwrite an attendance record.
/// Creates an AttendanceAdjustment row with Status=Approved (audit trail).
/// </summary>
public class OverrideAttendanceCommandHandler : IRequestHandler<OverrideAttendanceCommand, int>
{
    private readonly IAttendanceAdjustmentRepository _repository;
    private readonly IAttendanceRecordRepository _recordRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IClock _clock;

    public OverrideAttendanceCommandHandler(
        IAttendanceAdjustmentRepository repository,
        IAttendanceRecordRepository recordRepository,
        ICurrentUserService currentUser,
        IAttendanceEditLockChecker lockChecker,
        IClock clock)
    {
        _repository = repository;
        _recordRepository = recordRepository;
        _currentUser = currentUser;
        _lockChecker = lockChecker;
        _clock = clock;
    }

    public async Task<int> Handle(OverrideAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (string.IsNullOrWhiteSpace(request.OverrideReason))
            throw new DomainValidationException("OverrideReason is required.");

        await _lockChecker.EnsureCanModifyAsync(
            request.WorkDate, request.OverrideReason, cancellationToken);

        var record = await _recordRepository.GetByKeyAsync(
            request.EmployeeInformationId, request.WorkDate, request.ShiftTemplateId, cancellationToken)
            ?? throw new NotFoundException(
                $"AttendanceRecord for employee {request.EmployeeInformationId} on {request.WorkDate:yyyy-MM-dd} shift {request.ShiftTemplateId} not found.");

        var newCheckInAt = request.NewCheckInAt;
        var newCheckOutAt = request.NewCheckOutAt;
        if (!AttendanceTimeRules.HasValidObservedRange(newCheckInAt, newCheckOutAt))
            throw new DomainValidationException("NewCheckOutAt must be later than NewCheckInAt.");

        var adjustment = new AttendanceAdjustmentEntity
        {
            AttendanceRecordId = record.Id,
            EmployeeInformationId = request.EmployeeInformationId,
            NewStatus = request.NewStatus,
            NewCheckInAt = newCheckInAt,
            NewCheckOutAt = newCheckOutAt,
            Reason = request.OverrideReason.Trim(),
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.OverrideAndApplyAsync(adjustment, _clock.UtcNow, cancellationToken);
    }
}
