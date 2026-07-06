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

public class RecordAttendanceCommandHandler : IRequestHandler<RecordAttendanceCommand, int>
{
    private readonly IAttendanceRecordRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IContractRepository _contractRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkHourSplitter _splitter;
    private readonly IComplianceLookupService _complianceLookup;
    private readonly IAttendanceEditLockChecker _lockChecker;
    private readonly IClock _clock;

    public RecordAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        IShiftTemplateRepository shiftRepository,
        IContractRepository contractRepository,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IComplianceLookupService complianceLookup,
        IAttendanceEditLockChecker lockChecker,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _contractRepository = contractRepository;
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

        await _lockChecker.EnsureCanModifyAsync(
            request.WorkDate, request.OverrideReason, cancellationToken);

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

        var laborLaw = await _complianceLookup.GetLaborLawConstantsAtAsync(request.WorkDate, cancellationToken);
        var checkInAt = request.CheckInAt;
        var checkOutAt = request.CheckOutAt;
        if (!AttendanceTimeRules.HasValidObservedRange(checkInAt, checkOutAt))
            throw new DomainValidationException("CheckOutAt must be later than CheckInAt.");

        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter,
            request.Status,
            checkInAt,
            checkOutAt,
            laborLaw.NightStartTime,
            laborLaw.NightEndTime,
            shift);

        var entity = new AttendanceRecordEntity
        {
            EmployeeInformationId = request.EmployeeInformationId,
            WorkDate = request.WorkDate,
            ShiftTemplateId = request.ShiftTemplateId,
            CheckInAt = checkInAt,
            CheckOutAt = checkOutAt,
            DayHours = dayHours,
            NightHours = nightHours,
            Status = request.Status,
            Notes = request.Notes,
            OverrideReason = !string.IsNullOrWhiteSpace(request.OverrideReason)
                ? request.OverrideReason.Trim()
                : null,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
