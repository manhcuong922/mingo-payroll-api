using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public class ReapplyAttendanceRuleCommandHandler : IRequestHandler<ReapplyAttendanceRuleCommand, bool>
{
    private readonly IShiftTemplateRepository _shiftTemplateRepository;
    private readonly IAttendanceRuleRepository _attendanceRuleRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public ReapplyAttendanceRuleCommandHandler(
        IShiftTemplateRepository shiftTemplateRepository,
        IAttendanceRuleRepository attendanceRuleRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _shiftTemplateRepository = shiftTemplateRepository;
        _attendanceRuleRepository = attendanceRuleRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(ReapplyAttendanceRuleCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var template = await _shiftTemplateRepository.GetByIdAsync(request.ShiftTemplateId, cancellationToken)
            ?? throw new NotFoundException($"ShiftTemplate {request.ShiftTemplateId} not found.");

        var preset = await _attendanceRuleRepository.GetByIdAsync(request.AttendanceRuleId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRule {request.AttendanceRuleId} not found.");
        if (!preset.IsActive)
            throw new DomainValidationException($"AttendanceRule {request.AttendanceRuleId} is inactive and cannot be applied.");

        PayRuleFloor.EnsureValid(preset.WorkRateNormal, preset.WorkRateWeeklyRest, preset.WorkRateHoliday);

        var entity = new ShiftTemplateEntity
        {
            Id = template.Id,
            Code = template.Code,
            Name = template.Name,
            StartTime = template.StartTime,
            EndTime = template.EndTime,
            BreakStartTime = template.BreakStartTime,
            BreakEndTime = template.BreakEndTime,
            StandardHours = template.StandardHours,
            StandardDays = template.StandardDays,
            WorkRateNormal = preset.WorkRateNormal,
            WorkRateWeeklyRest = preset.WorkRateWeeklyRest,
            WorkRateHoliday = preset.WorkRateHoliday,
            IsNightShift = template.IsNightShift,
            AppliedAttendanceRuleId = request.AttendanceRuleId,
            IsActive = template.IsActive,
            Notes = template.Notes,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _shiftTemplateRepository.UpdateAsync(entity, cancellationToken);
    }
}
