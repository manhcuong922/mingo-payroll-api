using MediatR;
using MPS.Application.Common.Interfaces;
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
    private readonly IClock _clock;

    public OverrideAttendanceCommandHandler(
        IAttendanceAdjustmentRepository repository,
        IAttendanceRecordRepository recordRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _recordRepository = recordRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(OverrideAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (string.IsNullOrWhiteSpace(request.OverrideReason))
            throw new DomainValidationException("OverrideReason is required.");

        var record = await _recordRepository.GetByKeyAsync(
            request.EmployeeInformationId, request.WorkDate, request.ShiftTemplateId, cancellationToken)
            ?? throw new NotFoundException(
                $"AttendanceRecord for employee {request.EmployeeInformationId} on {request.WorkDate:yyyy-MM-dd} shift {request.ShiftTemplateId} not found.");

        var adjustment = new AttendanceAdjustmentEntity
        {
            AttendanceRecordId = record.Id,
            EmployeeInformationId = request.EmployeeInformationId,
            NewStatus = request.NewStatus,
            NewCheckInTime = request.NewCheckInTime,
            NewCheckOutTime = request.NewCheckOutTime,
            Reason = request.OverrideReason.Trim(),
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.OverrideAndApplyAsync(adjustment, _clock.UtcNow, cancellationToken);
    }
}
