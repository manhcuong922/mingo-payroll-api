using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public class UpdateShiftTemplateCommandHandler : IRequestHandler<UpdateShiftTemplateCommand, bool>
{
    private readonly IShiftTemplateRepository _repository;
    private readonly IAttendanceRuleRepository _attendanceRuleRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateShiftTemplateCommandHandler(
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

    public async Task<bool> Handle(UpdateShiftTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to update a shift template.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        CreateShiftTemplateCommandHandler.EnsureValidCode(code);

        if (await _repository.ExistsByCodeAsync(code, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Shift template with code '{code}' already exists.");

        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Shift template with name '{name}' already exists.");

        if (request.EndTime <= request.StartTime)
            throw new DomainValidationException("EndTime must be greater than StartTime.");

        CreateShiftTemplateCommandHandler.ValidateBreakAndStandardHours(
            request.StartTime,
            request.EndTime,
            request.BreakStartTime,
            request.BreakEndTime,
            request.StandardHours);

        if (request.AppliedAttendanceRuleId is { } presetId)
        {
            var preset = await _attendanceRuleRepository.GetByIdAsync(presetId, cancellationToken);
            if (preset is null)
                throw new NotFoundException($"AttendanceRule {presetId} not found.");
        }

        var entity = new ShiftTemplateEntity
        {
            Id = request.Id,
            Code = code,
            Name = name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            BreakStartTime = request.BreakStartTime,
            BreakEndTime = request.BreakEndTime,
            StandardHours = request.StandardHours,
            StandardDays = request.StandardDays,
            WorkRateNormal = existing.WorkRateNormal,
            WorkRateWeeklyRest = existing.WorkRateWeeklyRest,
            WorkRateHoliday = existing.WorkRateHoliday,
            IsNightShift = request.IsNightShift,
            AppliedAttendanceRuleId = request.AppliedAttendanceRuleId,
            IsActive = request.IsActive,
            Notes = request.Notes,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
