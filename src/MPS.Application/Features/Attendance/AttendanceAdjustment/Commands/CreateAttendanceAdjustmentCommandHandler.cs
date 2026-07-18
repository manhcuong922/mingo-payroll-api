using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Attendance.Common;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using AttendanceAdjustmentEntity = MPS.Domain.Entities.Attendance.AttendanceAdjustment;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Commands;

/// <summary>
/// FT-14: Employee creates an adjustment request for an existing AttendanceRecord.
/// BR-11: Reason is required.
/// BR-12: At most one Pending adjustment per record.
/// </summary>
public class CreateAttendanceAdjustmentCommandHandler : IRequestHandler<CreateAttendanceAdjustmentCommand, int>
{
    private readonly IAttendanceAdjustmentRepository _repository;
    private readonly IAttendanceRecordRepository _recordRepository;
    private readonly IContractLookupReader _lookup;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateAttendanceAdjustmentCommandHandler(
        IAttendanceAdjustmentRepository repository,
        IAttendanceRecordRepository recordRepository,
        IContractLookupReader lookup,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _recordRepository = recordRepository;
        _lookup = lookup;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateAttendanceAdjustmentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");
        var employeeId = await _lookup.GetEmployeeInformationIdByUserIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"No employee profile linked to user {userId}.");

        var record = await _recordRepository.GetByIdAsync(request.AttendanceRecordId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRecord {request.AttendanceRecordId} not found.");

        if (record.EmployeeInformationId != employeeId)
            throw new ForbiddenAccessException("You can only request adjustments for your own attendance records.");

        // BR-12
        var pending = await _repository.GetPendingByRecordIdAsync(request.AttendanceRecordId, cancellationToken);
        if (pending is not null)
            throw new ConflictException(
                $"A pending adjustment already exists for AttendanceRecord {request.AttendanceRecordId}.");

        var newCheckInAt = request.NewCheckInAt;
        var newCheckOutAt = request.NewCheckOutAt;
        if (!AttendanceTimeRules.HasValidObservedRange(newCheckInAt, newCheckOutAt))
            throw new DomainValidationException("NewCheckOutAt must be later than NewCheckInAt.");

        var entity = new AttendanceAdjustmentEntity
        {
            AttendanceRecordId = request.AttendanceRecordId,
            EmployeeInformationId = employeeId,
            NewStatus = request.NewStatus,
            NewCheckInAt = newCheckInAt,
            NewCheckOutAt = newCheckOutAt,
            Reason = request.Reason.Trim(),
            Status = AdjustmentStatus.Pending,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
