using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.ScheduleTemplate.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using ScheduleTemplateEntity = MPS.Domain.Entities.Attendance.ScheduleTemplate;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public class UpdateScheduleTemplateCommandHandler
    : IRequestHandler<UpdateScheduleTemplateCommand, Unit>
{
    private readonly IScheduleTemplateRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IShiftTemplateRepository shiftRepository,
        IScheduleTemplateExpander expander,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _expander = expander;
        _assignmentRepository = assignmentRepository;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Unit> Handle(UpdateScheduleTemplateCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

        var existing = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"ScheduleTemplate {request.Id} not found.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
        {
            throw new DomainValidationException("Tên bảng phân ca không được rỗng.");
        }

        ScheduleTemplateValidation.EnsureValidShape(
            request.EffectiveFrom,
            request.EffectiveTo,
            request.RepeatUnit,
            request.RepeatEvery,
            request.DaysOfWeek,
            request.DaysOfMonth,
            request.EmployeeInformationIds);

        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
        {
            throw new ConflictException($"Bảng phân ca với tên '{name}' đã tồn tại.");
        }

        // Multi-shift: cần shift mới để check overlap theo thời gian. Load luôn (kể cả khi ShiftId không đổi
        // vì đang refactor — đơn giản hơn).
        var shift = await _shiftRepository.GetByIdAsync(request.ShiftTemplateId, cancellationToken)
            ?? throw new NotFoundException($"Shift template {request.ShiftTemplateId} not found.");
        if (!shift.IsActive)
        {
            throw new DomainValidationException($"Shift template {request.ShiftTemplateId} không active.");
        }

        // Hướng mới: pattern-date overlap check chỉ chặn khi ca mới giao thoa thời gian với ca đã có.
        var newShape = new ScheduleTemplateEntity
        {
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            RepeatUnit = request.RepeatUnit,
            RepeatEvery = request.RepeatEvery,
            DaysOfWeekMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfWeek),
            DaysOfMonthMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfMonth),
        };
        await EnsureNoPatternOverlapAsync(
            request.EmployeeInformationIds,
            newShape,
            shift.StartTime,
            shift.EndTime,
            excludeTemplateId: request.Id,
            cancellationToken);

        existing.Name = name;
        existing.ShiftTemplateId = request.ShiftTemplateId;
        existing.EffectiveFrom = request.EffectiveFrom;
        existing.EffectiveTo = request.EffectiveTo;
        existing.RepeatUnit = request.RepeatUnit;
        existing.RepeatEvery = request.RepeatEvery;
        existing.DaysOfWeekMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfWeek);
        existing.DaysOfMonthMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfMonth);
        existing.IsActive = request.IsActive;
        existing.Notes = request.Notes;
        existing.UpdatedAt = _clock.UtcNow;
        existing.UpdatedByUserId = userId;

        await _repository.UpdateAsync(existing, request.EmployeeInformationIds, cancellationToken);

        return Unit.Value;
    }

    private async Task EnsureNoPatternOverlapAsync(
        IReadOnlyList<int> employeeIds,
        ScheduleTemplateEntity templateShape,
        TimeOnly newShiftStart,
        TimeOnly newShiftEnd,
        int? excludeTemplateId,
        CancellationToken cancellationToken)
    {
        var dates = _expander.ExpandDates(templateShape);
        if (dates.Count == 0 || employeeIds.Count == 0) return;

        var pairs = employeeIds
            .SelectMany(empId => dates.Select(d => (EmployeeInformationId: empId, WorkDate: d)))
            .ToList();

        var conflicts = await _assignmentRepository.GetOwnedConflictsAsync(
            pairs, excludeTemplateId, newShiftStart, newShiftEnd, cancellationToken);
        if (conflicts.Count == 0) return;

        var summary = conflicts
            .GroupBy(c => c.OtherScheduleTemplateName)
            .Select(g => $"\"{g.Key}\" ({g.Count()} ngày)")
            .Take(3);
        throw new ConflictException(
            $"Không thể cập nhật: ca mới trùng giờ với {string.Join(", ", summary)}.");
    }
}
