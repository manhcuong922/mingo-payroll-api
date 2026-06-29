using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
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

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Tên ca làm việc không được rỗng.");

        if (await _repository.ExistsByNameAsync(name, excludeId: null, cancellationToken))
            throw new ConflictException($"Shift template with name '{name}' already exists.");

        var code = await ShiftTemplateCodeGenerator.GenerateUniqueAsync(
            name,
            _repository,
            excludeId: null,
            cancellationToken);

        if (request.EndTime <= request.StartTime)
            throw new DomainValidationException("EndTime must be greater than StartTime.");

        ValidateBreakAndStandardHours(
            request.StartTime,
            request.EndTime,
            request.BreakStartTime,
            request.BreakEndTime,
            request.StandardHours);

        var rule = await _attendanceRuleRepository.GetByIdAsync(request.AppliedAttendanceRuleId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRule {request.AppliedAttendanceRuleId} not found.");

        if (!rule.IsActive)
            throw new DomainValidationException($"AttendanceRule {rule.Id} is inactive and cannot be applied.");

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
            AppliedAttendanceRuleId = rule.Id,
            IsActive = true,
            Notes = request.Notes,
            CreatedAt = now,
            CreatedByUserId = userId
        };

        return await _repository.AddAsync(entity, cancellationToken);
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

        if (standardHours > RegularHoursCap.MaxPerDay)
            throw new DomainValidationException(
                $"StandardHours ({standardHours}) vượt cap {RegularHoursCap.MaxPerDay}h/ngày — Điều 105.1 BLLĐ.");
    }
}
