using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.ScheduleTemplate.Common;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using ScheduleTemplateEntity = MPS.Domain.Entities.Attendance.ScheduleTemplate;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public class UpdateScheduleTemplateCommandHandler
    : IRequestHandler<UpdateScheduleTemplateCommand, Unit>
{
    private readonly IScheduleTemplateRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IShiftTemplateRepository shiftRepository,
        IScheduleTemplateExpander expander,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        IContractRepository contractRepository,
        IEmployeeInformationRepository employeeRepository,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _expander = expander;
        _assignmentRepository = assignmentRepository;
        _contractRepository = contractRepository;
        _employeeRepository = employeeRepository;
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

        var employeeIds = await ScheduleTemplateEmployeeResolver.ResolveAsync(
            request.ApplyTarget,
            request.EmployeeInformationIds,
            request.DepartmentIds,
            _employeeRepository,
            requireEmployees: false,
            cancellationToken);

        ScheduleTemplateValidation.EnsureValidShape(
            request.EffectiveFrom,
            request.EffectiveTo,
            request.RepeatUnit,
            request.RepeatEvery,
            request.DaysOfWeek,
            request.DaysOfMonth,
            employeeIds,
            requireEmployees: false);

        if (await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
        {
            throw new ConflictException($"Bảng phân ca với tên '{name}' đã tồn tại.");
        }

        // Multi-shift: cần shift mới để check overlap theo thời gian. Load luôn (kể cả khi ShiftId không đổi
        // vì đang refactor — đơn giản hơn).
        var shifts = await LoadActiveShiftsAsync(request.ShiftTemplateIds, cancellationToken);

        await EnsureEmployeesHaveActiveContractsAsync(employeeIds, cancellationToken);

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
        EnsureSelectedShiftsDoNotOverlapOnPattern(newShape, shifts);
        await EnsureNoPatternOverlapAsync(
            employeeIds,
            newShape,
            shifts,
            excludeTemplateId: request.Id,
            cancellationToken);

        existing.Name = name;
        existing.ShiftTemplateId = shifts[0].Id;
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

        await _repository.UpdateAsync(existing, employeeIds, shifts.Select(s => s.Id).ToList(), cancellationToken);

        return Unit.Value;
    }

    private async Task<IReadOnlyList<ShiftTemplateEntity>> LoadActiveShiftsAsync(
        IReadOnlyCollection<int> shiftTemplateIds,
        CancellationToken cancellationToken)
    {
        var ids = shiftTemplateIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (ids.Count == 0)
        {
            throw new DomainValidationException("Chọn ít nhất 1 ca làm việc.");
        }

        var shifts = new List<ShiftTemplateEntity>(ids.Count);
        foreach (var id in ids)
        {
            var shift = await _shiftRepository.GetByIdAsync(id, cancellationToken)
                ?? throw new NotFoundException($"Shift template {id} not found.");
            if (!shift.IsActive)
            {
                throw new DomainValidationException($"Shift template {id} không active.");
            }
            shifts.Add(shift);
        }

        return shifts
            .OrderBy(s => s.StartTime)
            .ThenBy(s => s.Code)
            .ToList();
    }

    private async Task EnsureEmployeesHaveActiveContractsAsync(
        IReadOnlyCollection<int> employeeIds,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0) return;

        var activeIds = await _contractRepository.GetEmployeeInformationIdsWithActiveContractsAsync(ids, cancellationToken);
        var activeSet = activeIds.ToHashSet();
        var inactiveIds = ids.Where(id => !activeSet.Contains(id)).ToArray();
        if (inactiveIds.Length == 0) return;

        var preview = string.Join(", ", inactiveIds.Take(10));
        var suffix = inactiveIds.Length > 10 ? $" và {inactiveIds.Length - 10} nhân viên khác" : string.Empty;
        throw new DomainValidationException(
            $"Không thể cập nhật bảng phân ca: nhân viên {preview}{suffix} chưa có hợp đồng active.");
    }

    private async Task EnsureNoPatternOverlapAsync(
        IReadOnlyList<int> employeeIds,
        ScheduleTemplateEntity templateShape,
        IReadOnlyList<ShiftTemplateEntity> shifts,
        int? excludeTemplateId,
        CancellationToken cancellationToken)
    {
        var dates = _expander.ExpandDates(templateShape);
        if (dates.Count == 0 || employeeIds.Count == 0) return;

        var pairs = employeeIds
            .SelectMany(empId => dates.Select(d => (EmployeeInformationId: empId, WorkDate: d)))
            .ToList();

        foreach (var shift in shifts)
        {
            var conflicts = await _assignmentRepository.GetOwnedConflictsAsync(
                pairs, excludeTemplateId, shift.StartTime, shift.EndTime, cancellationToken);
            if (conflicts.Count == 0) continue;

            var summary = conflicts
                .GroupBy(c => c.OtherScheduleTemplateName)
                .Select(g => $"\"{g.Key}\" ({g.Count()} ngày)")
                .Take(3);
            throw new ConflictException(
                $"Không thể cập nhật: ca \"{shift.Name}\" trùng giờ với {string.Join(", ", summary)}.");
        }
    }

    private void EnsureSelectedShiftsDoNotOverlapOnPattern(
        ScheduleTemplateEntity templateShape,
        IReadOnlyList<ShiftTemplateEntity> shifts)
    {
        if (shifts.Count < 2) return;

        var dates = _expander.ExpandDates(templateShape);
        if (dates.Count == 0) return;

        var dateSet = dates.ToHashSet();
        foreach (var date in dates)
        {
            EnsureSelectedShiftsDoNotOverlap(shifts, date, date);

            var nextDate = date.AddDays(1);
            if (dateSet.Contains(nextDate))
            {
                EnsureSelectedShiftsDoNotOverlap(shifts, date, nextDate);
            }
        }
    }

    private static void EnsureSelectedShiftsDoNotOverlap(
        IReadOnlyList<ShiftTemplateEntity> shifts,
        DateOnly leftDate,
        DateOnly rightDate)
    {
        for (var i = 0; i < shifts.Count; i++)
        for (var j = i + 1; j < shifts.Count; j++)
        {
            var left = shifts[i];
            var right = shifts[j];
            if (!ShiftTimeCalculator.Overlaps(leftDate, left.StartTime, left.EndTime, rightDate, right.StartTime, right.EndTime))
            {
                continue;
            }

            throw new DomainValidationException(
                $"Hai ca \"{left.Name}\" và \"{right.Name}\" trùng giờ, không thể cùng nằm trong một bảng phân ca.");
        }
    }
}
