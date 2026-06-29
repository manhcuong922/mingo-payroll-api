using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
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
    private readonly IRegularHoursCapChecker _capChecker;
    private readonly IClock _clock;

    public RecordAttendanceCommandHandler(
        IAttendanceRecordRepository repository,
        IShiftTemplateRepository shiftRepository,
        IContractRepository contractRepository,
        ICurrentUserService currentUser,
        IWorkHourSplitter splitter,
        IRegularHoursCapChecker capChecker,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _contractRepository = contractRepository;
        _currentUser = currentUser;
        _splitter = splitter;
        _capChecker = capChecker;
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

        var (dayHours, nightHours) = AttendanceHourCalculator.Compute(
            _splitter, request.Status, request.CheckInTime, request.CheckOutTime, shift);

        // §9 Phase 1b — Cap 48h/tuần warn-then-confirm (Điều 105 BLLĐ).
        if (!request.AcknowledgeWeeklyOvertimeWarnings)
        {
            var delta = dayHours + nightHours;
            if (delta > 0)
            {
                var deltas = new[] { new WeeklyCapDelta(request.EmployeeInformationId, request.WorkDate, delta) };
                var dailyWarnings = await _capChecker.CheckDailyAttendanceAsync(
                    deltas,
                    cancellationToken);
                var weeklyWarnings = await _capChecker.CheckAttendanceAsync(
                    deltas,
                    cancellationToken);
                if (dailyWarnings.Count > 0 || weeklyWarnings.Count > 0)
                    throw new RegularHoursCapWarningException(dailyWarnings, weeklyWarnings);
            }
        }

        var entity = new AttendanceRecordEntity
        {
            EmployeeInformationId = request.EmployeeInformationId,
            WorkDate = request.WorkDate,
            ShiftTemplateId = request.ShiftTemplateId,
            CheckInTime = request.CheckInTime,
            CheckOutTime = request.CheckOutTime,
            DayHours = dayHours,
            NightHours = nightHours,
            Status = request.Status,
            Notes = request.Notes,
            CreatedAt = _clock.UtcNow,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }
}
