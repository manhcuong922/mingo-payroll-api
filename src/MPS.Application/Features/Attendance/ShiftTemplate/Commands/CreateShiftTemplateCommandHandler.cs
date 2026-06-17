using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using System.Text.RegularExpressions;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public class CreateShiftTemplateCommandHandler : IRequestHandler<CreateShiftTemplateCommand, int>
{
    private readonly IShiftTemplateRepository _repository;
    private readonly IAttendanceRuleRepository _attendanceRuleRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateShiftTemplateCommandHandler(
        IShiftTemplateRepository repository,
        IAttendanceRuleRepository attendanceRuleRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _attendanceRuleRepository = attendanceRuleRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<int> Handle(CreateShiftTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to create a shift template.");

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        EnsureValidCode(code);

        if (await _repository.ExistsByCodeAsync(code, excludeId: null, cancellationToken))
            throw new ConflictException($"Shift template with code '{code}' already exists.");

        if (await _repository.ExistsByNameAsync(name, excludeId: null, cancellationToken))
            throw new ConflictException($"Shift template with name '{name}' already exists.");

        if (request.EndTime <= request.StartTime)
            throw new DomainValidationException("EndTime must be greater than StartTime.");

        ValidateBreakAndStandardHours(
            request.StartTime,
            request.EndTime,
            request.BreakStartTime,
            request.BreakEndTime,
            request.StandardHours);

        var (workRateNormal, workRateWeeklyRest, workRateHoliday) = await ResolveWorkRatesAsync(
            request,
            cancellationToken);

        PayRuleFloor.EnsureValid(workRateNormal, workRateWeeklyRest, workRateHoliday);

        var now = _clock.UtcNow;
        var entity = new ShiftTemplateEntity
        {
            Code = code,
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BreakStartTime = request.BreakStartTime,
            BreakEndTime = request.BreakEndTime,
            StandardHours = request.StandardHours,
            StandardDays = request.StandardDays,
            WorkRateNormal = workRateNormal,
            WorkRateWeeklyRest = workRateWeeklyRest,
            WorkRateHoliday = workRateHoliday,
            IsNightShift = request.IsNightShift,
            AppliedAttendanceRuleId = request.AppliedAttendanceRuleId,
            IsActive = true,
            Notes = request.Notes,
            CreatedAt = now,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
    }

    private async Task<(decimal Normal, decimal WeeklyRest, decimal Holiday)> ResolveWorkRatesAsync(
        CreateShiftTemplateCommand request,
        CancellationToken cancellationToken)
    {
        if (request.AppliedAttendanceRuleId is not { } presetId)
            return (request.WorkRateNormal, request.WorkRateWeeklyRest, request.WorkRateHoliday);

        var preset = await _attendanceRuleRepository.GetByIdAsync(presetId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRule {presetId} not found.");

        if (!preset.IsActive)
            throw new DomainValidationException($"AttendanceRule {presetId} is inactive and cannot be applied.");

        return (preset.WorkRateNormal, preset.WorkRateWeeklyRest, preset.WorkRateHoliday);
    }

    internal static void EnsureValidCode(string code)
    {
        if (!Regex.IsMatch(code, "^[A-Z0-9-]{2,20}$"))
            throw new DomainValidationException(
                "BR-29: Code must be 2-20 chars and contain only uppercase letters, digits, and hyphens.");
    }

    internal static void ValidateBreakAndStandardHours(
        TimeOnly startTime,
        TimeOnly endTime,
        TimeOnly? breakStartTime,
        TimeOnly? breakEndTime,
        decimal standardHours)
    {
        if (breakStartTime.HasValue != breakEndTime.HasValue)
            throw new DomainValidationException("BR-28: BreakStartTime and BreakEndTime must be set together.");

        var netHours = (decimal)(endTime - startTime).TotalHours;

        if (breakStartTime.HasValue && breakEndTime.HasValue)
        {
            var breakStart = breakStartTime.Value;
            var breakEnd = breakEndTime.Value;

            if (breakEnd <= breakStart)
                throw new DomainValidationException("BR-28: BreakEndTime must be greater than BreakStartTime.");

            if (breakStart < startTime || breakStart > endTime || breakEnd < startTime || breakEnd > endTime)
                throw new DomainValidationException("BR-28: Break time must be within the shift time range.");

            netHours -= (decimal)(breakEnd - breakStart).TotalHours;
        }

        if (standardHours > netHours)
            throw new DomainValidationException(
                $"StandardHours ({standardHours}) cannot exceed net shift hours ({netHours}).");
    }
}
