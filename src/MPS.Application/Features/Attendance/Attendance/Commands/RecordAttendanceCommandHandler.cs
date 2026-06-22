using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using AttendanceRecordEntity = MPS.Domain.Entities.Attendance.AttendanceRecord;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

public class RecordAttendanceCommandHandler : IRequestHandler<RecordAttendanceCommand, int>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public RecordAttendanceCommandHandler(
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

    public async Task<int> Handle(RecordAttendanceCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        if (!await _contractRepository.HasActiveContractAsync(request.EmployeeInformationId, cancellationToken))
            throw new DomainValidationException(
                $"Employee {request.EmployeeInformationId} has no active contract; cannot record attendance.");

        var shift = await _shiftRepository.GetByIdAsync(request.ShiftTemplateId, cancellationToken)
            ?? throw new DomainValidationException(
                $"ShiftTemplate {request.ShiftTemplateId} not found or inactive.");
        if (!shift.IsActive)
            throw new DomainValidationException(
                $"ShiftTemplate '{shift.Code}' is inactive; cannot record attendance.");

        var existing = await _repository.GetByKeyAsync(
            request.EmployeeInformationId, request.WorkDate, request.ShiftTemplateId, cancellationToken);
        if (existing is not null)
            throw new ConflictException(
                $"Attendance record already exists for employee {request.EmployeeInformationId} on {request.WorkDate:yyyy-MM-dd} shift {shift.Code}.");

        var entity = new AttendanceRecordEntity
        {
            EmployeeInformationId = request.EmployeeInformationId,
            WorkDate = request.WorkDate,
            ShiftTemplateId = request.ShiftTemplateId,
            CheckInTime = request.CheckInTime,
            CheckOutTime = request.CheckOutTime,
            Status = request.Status,
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
