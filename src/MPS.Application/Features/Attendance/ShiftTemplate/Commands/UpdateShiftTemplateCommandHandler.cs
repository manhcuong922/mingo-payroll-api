using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;
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
    private readonly IComplianceLookupService _complianceLookup;

    public UpdateShiftTemplateCommandHandler(
        IShiftTemplateRepository repository,
        IAttendanceRuleRepository attendanceRuleRepository,
        ICurrentUserService currentUser,
        IClock clock,
        IComplianceLookupService complianceLookup)
    {
        _repository = repository;
        _attendanceRuleRepository = attendanceRuleRepository;
        _currentUser = currentUser;
        _clock = clock;
        _complianceLookup = complianceLookup;
    }

    public async Task<bool> Handle(UpdateShiftTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required to update a shift template.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (existing is null) return false;

        if (existing.IsActive && !request.IsActive)
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow);
            var usage = await _repository.GetFutureUsageAsync(request.Id, today, cancellationToken);
            if (usage.HasAny)
                throw new ConflictException(ShiftTemplateFutureUsageMessage.Build(existing.Name, usage));
        }

        var name = request.Name.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainValidationException("Tên ca làm việc không được rỗng.");

        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
            throw new ConflictException($"Shift template with name '{name}' already exists.");

        var code = await ShiftTemplateCodeGenerator.GenerateUniqueAsync(
            name,
            _repository,
            excludeId: request.Id,
            cancellationToken);

        var laborLaw = await _complianceLookup.GetActiveLaborLawConstantsAsync(cancellationToken);

        CreateShiftTemplateCommandHandler.ValidateBreakAndStandardHours(
            request.StartTime,
            request.EndTime,
            request.BreakStartTime,
            request.BreakEndTime,
            request.StandardHours,
            laborLaw.StandardWorkingHoursPerDay);

        var rule = await _attendanceRuleRepository.GetByIdAsync(request.AppliedAttendanceRuleId, cancellationToken)
            ?? throw new NotFoundException($"AttendanceRule {request.AppliedAttendanceRuleId} not found.");

        if (!rule.IsActive)
            throw new DomainValidationException($"AttendanceRule {rule.Id} is inactive and cannot be applied.");

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
            AppliedAttendanceRuleId = rule.Id,
            IsActive = request.IsActive,
            Notes = request.Notes,
            UpdatedAt = _clock.UtcNow,
            UpdatedByUserId = userId
        };

        return await _repository.UpdateAsync(entity, cancellationToken);
    }
}
