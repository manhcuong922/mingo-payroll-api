using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ShiftTemplate.Commands;

public class UpdateShiftPayRatesCommandHandler : IRequestHandler<UpdateShiftPayRatesCommand, bool>
{
    private readonly IShiftTemplateRepository _shiftTemplateRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateShiftPayRatesCommandHandler(
        IShiftTemplateRepository shiftTemplateRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _shiftTemplateRepository = shiftTemplateRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<bool> Handle(UpdateShiftPayRatesCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var template = await _shiftTemplateRepository.GetByIdAsync(request.ShiftTemplateId, cancellationToken);
        if (template is null) return false;

        PayRuleFloor.EnsureValid(
            request.WorkRateNormal,
            request.WorkRateWeeklyRest,
            request.WorkRateHoliday);

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
            WorkRateNormal = request.WorkRateNormal,
            WorkRateWeeklyRest = request.WorkRateWeeklyRest,
            WorkRateHoliday = request.WorkRateHoliday,
            IsNightShift = template.IsNightShift,
            AppliedAttendanceRuleId = template.AppliedAttendanceRuleId,
            IsActive = template.IsActive,
            Notes = template.Notes,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _shiftTemplateRepository.UpdateAsync(entity, cancellationToken);
    }
}
