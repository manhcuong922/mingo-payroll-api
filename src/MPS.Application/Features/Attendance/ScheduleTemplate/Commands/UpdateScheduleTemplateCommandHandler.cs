using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Application.Features.Attendance.ScheduleTemplate.Common;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;
using MPS.Domain.Enums.Attendance;
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
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly IEmployeeInformationRepository _employeeRepository;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public UpdateScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IShiftTemplateRepository shiftRepository,
        IDepartmentRepository departmentRepository,
        IScheduleTemplateExpander expander,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        IEmployeeInformationRepository employeeRepository,
        IMediator mediator,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _departmentRepository = departmentRepository;
        _expander = expander;
        _assignmentRepository = assignmentRepository;
        _employeeRepository = employeeRepository;
        _mediator = mediator;
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

        if (string.IsNullOrEmpty(name))
        {
            // Auto-sinh nếu user xoá tên — luôn unique (loại trừ chính template đang sửa).
            name = await ScheduleTemplateNameGenerator.GenerateAsync(
                request.ShiftTemplateIds,
                request.ApplyTarget,
                request.EmployeeInformationIds,
                request.DepartmentIds,
                request.EffectiveFrom,
                request.EffectiveTo,
                excludeTemplateId: request.Id,
                _shiftRepository,
                _departmentRepository,
                _employeeRepository,
                _repository,
                cancellationToken);
        }
        else if (request.IsActive
            && await _repository.ExistsByNameAsync(name, excludeId: request.Id, cancellationToken))
        {
            // Chỉ check trùng khi request muốn activate. Nếu giữ inactive → DB filtered unique index
            // (§12) không enforce trên inactive rows, không cần chặn ở app layer. Xem §12.7.
            throw new ConflictException($"Bảng phân ca với tên '{name}' đã tồn tại.");
        }

        // Multi-shift: cần shift mới để check overlap theo thời gian. Load luôn (kể cả khi ShiftId không đổi
        // vì đang refactor — đơn giản hơn).
        var shifts = await LoadActiveShiftsAsync(request.ShiftTemplateIds, cancellationToken);

        await EnsureEmployeesHaveActiveUsersAsync(employeeIds, cancellationToken);

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

        var applyTarget = request.ApplyTarget ?? ScheduleApplyTarget.Employees;
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
        existing.IncludeRestDaysAndHolidays = request.IncludeRestDaysAndHolidays;
        existing.ApplyTarget = applyTarget;
        existing.TargetDepartmentIds = applyTarget == ScheduleApplyTarget.Departments
            ? ScheduleTemplateValidation.ToCsvMask(request.DepartmentIds)
            : null;
        existing.UpdatedAt = _clock.UtcNow;
        existing.UpdatedByUserId = userId;

        var shiftIds = shifts.Select(s => s.Id).ToList();
        await _repository.UpdateAsync(existing, employeeIds, shiftIds, cancellationToken);

        // §15: Sync assignments với pattern mới. Sweep stale (giữ locked) + re-expand nếu active.
        // Update = Save + Sync (không cần user bấm Reapply riêng).
        var dates = _expander.ExpandDates(existing);
        await _assignmentRepository.DeleteStaleByScheduleTemplateAsync(
            existing.Id, dates, shiftIds, employeeIds, cancellationToken);

        if (request.IsActive && dates.Count > 0 && employeeIds.Count > 0 && shiftIds.Count > 0)
        {
            var operations = new List<BatchShiftOperation>(dates.Count * employeeIds.Count * shiftIds.Count);
            foreach (var empId in employeeIds)
            foreach (var date in dates)
            foreach (var shiftId in shiftIds)
                operations.Add(new BatchShiftOperation(
                    BatchShiftOperationKind.Add, empId, date, shiftId));

            await _mediator.Send(
                new BatchUpdateShiftAssignmentsCommand(
                    operations,
                    BatchShiftConflictPolicy.ReplaceIfNoAttendance,
                    SourceScheduleTemplateId: existing.Id,
                    AcknowledgeWeeklyOvertimeWarnings: request.AcknowledgeWeeklyOvertimeWarnings,
                    IncludeRestDaysAndHolidays: request.IncludeRestDaysAndHolidays),
                cancellationToken);
        }

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

    private async Task EnsureEmployeesHaveActiveUsersAsync(
        IReadOnlyCollection<int> employeeIds,
        CancellationToken cancellationToken)
    {
        var ids = employeeIds.Distinct().OrderBy(id => id).ToArray();
        if (ids.Length == 0) return;

        var activeIds = await _employeeRepository.GetEmployeeInformationIdsWithActiveUserAsync(ids, cancellationToken);
        var activeSet = activeIds.ToHashSet();
        var inactiveIds = ids.Where(id => !activeSet.Contains(id)).ToArray();
        if (inactiveIds.Length == 0) return;

        var preview = string.Join(", ", inactiveIds.Take(10));
        var suffix = inactiveIds.Length > 10 ? $" và {inactiveIds.Length - 10} nhân viên khác" : string.Empty;
        throw new DomainValidationException(
            $"Không thể cập nhật bảng phân ca: nhân viên {preview}{suffix} không có tài khoản active.");
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
