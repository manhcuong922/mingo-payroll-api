using MediatR;
using MPS.Application.Common.Exceptions;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Application.Features.Attendance.ScheduleTemplate.Common;
using MPS.Application.Features.Attendance.ShiftTemplate.Common;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;
using ScheduleTemplateEntity = MPS.Domain.Entities.Attendance.ScheduleTemplate;
using ShiftTemplateEntity = MPS.Domain.Entities.Attendance.ShiftTemplate;
using MPS.Domain.Entities.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public class CreateScheduleTemplateCommandHandler
    : IRequestHandler<CreateScheduleTemplateCommand, ScheduleTemplateApplyResultDto>
{
    private readonly IScheduleTemplateRepository _repository;
    private readonly IShiftTemplateRepository _shiftRepository;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IRegularHoursCapChecker _capChecker;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IClock _clock;

    public CreateScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IShiftTemplateRepository shiftRepository,
        IScheduleTemplateExpander expander,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        IContractRepository contractRepository,
        IRegularHoursCapChecker capChecker,
        IMediator mediator,
        ICurrentUserService currentUser,
        IClock clock)
    {
        _repository = repository;
        _shiftRepository = shiftRepository;
        _expander = expander;
        _assignmentRepository = assignmentRepository;
        _contractRepository = contractRepository;
        _capChecker = capChecker;
        _mediator = mediator;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ScheduleTemplateApplyResultDto> Handle(
        CreateScheduleTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId
            ?? throw new ForbiddenAccessException("Authenticated user context is required.");

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

        if (await _repository.ExistsByNameAsync(name, excludeId: null, cancellationToken))
        {
            throw new ConflictException($"Bảng phân ca với tên '{name}' đã tồn tại.");
        }

        var shifts = await LoadActiveShiftsAsync(request.ShiftTemplateIds, cancellationToken);

        await EnsureEmployeesHaveActiveContractsAsync(request.EmployeeInformationIds, cancellationToken);

        // Build a transient shape for pattern expansion (entity not yet persisted).
        var templateShape = new ScheduleTemplateEntity
        {
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            RepeatUnit = request.RepeatUnit,
            RepeatEvery = request.RepeatEvery,
            DaysOfWeekMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfWeek),
            DaysOfMonthMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfMonth),
        };
        EnsureSelectedShiftsDoNotOverlapOnPattern(templateShape, shifts);
        await EnsureNoPatternOverlapAsync(
            request.EmployeeInformationIds,
            templateShape,
            shifts,
            excludeTemplateId: null,
            cancellationToken);

        // Pre-check cap before AddAsync to avoid creating an orphan ScheduleTemplate row
        // when shift assignments would be blocked.
        await EnsureRegularHoursCapAsync(
            request.EmployeeInformationIds,
            templateShape,
            shifts,
            request.AcknowledgeWeeklyOvertimeWarnings,
            cancellationToken);

        var now = _clock.UtcNow;
        var entity = new ScheduleTemplateEntity
        {
            Name = name,
            ShiftTemplateId = shifts[0].Id,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            RepeatUnit = request.RepeatUnit,
            RepeatEvery = request.RepeatEvery,
            DaysOfWeekMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfWeek),
            DaysOfMonthMask = ScheduleTemplateValidation.ToCsvMask(request.DaysOfMonth),
            IsActive = true,
            Notes = request.Notes,
            CreatedAt = now,
            CreatedByUserId = userId
        };

        var shiftIds = shifts.Select(s => s.Id).ToList();
        var id = await _repository.AddAsync(entity, request.EmployeeInformationIds, shiftIds, cancellationToken);
        entity.Id = id;

        return await ApplyAsync(entity, request.EmployeeInformationIds, cancellationToken);
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
            $"Không thể tạo bảng phân ca: nhân viên {preview}{suffix} chưa có hợp đồng active.");
    }

    private async Task EnsureRegularHoursCapAsync(
        IReadOnlyList<int> employeeIds,
        ScheduleTemplateEntity templateShape,
        IReadOnlyList<ShiftTemplateEntity> shifts,
        bool acknowledgeWarnings,
        CancellationToken cancellationToken)
    {
        var dates = _expander.ExpandDates(templateShape);
        if (dates.Count == 0 || employeeIds.Count == 0) return;

        var deltas = new List<WeeklyCapDelta>(dates.Count * employeeIds.Count * shifts.Count);
        foreach (var empId in employeeIds)
        foreach (var date in dates)
        foreach (var shift in shifts)
            deltas.Add(new WeeklyCapDelta(empId, date, shift.StandardHours));

        var dailyWarnings = await _capChecker.CheckDailyAssignmentsAsync(deltas, cancellationToken);
        var weeklyWarnings = await _capChecker.CheckAssignmentsAsync(deltas, cancellationToken);
        if (!acknowledgeWarnings && (dailyWarnings.Count > 0 || weeklyWarnings.Count > 0))
        {
            throw new RegularHoursCapExceededException(dailyWarnings, weeklyWarnings);
        }
    }

    private async Task<ScheduleTemplateApplyResultDto> ApplyAsync(
        ScheduleTemplateEntity template,
        IReadOnlyCollection<int> employeeIds,
        CancellationToken cancellationToken)
    {
        var dates = _expander.ExpandDates(template);
        if (dates.Count == 0 || employeeIds.Count == 0)
        {
            return new ScheduleTemplateApplyResultDto(
                template.Id, dates.Count, 0, 0, 0, Array.Empty<ScheduleTemplateApplyConflictDto>());
        }

        var shiftIds = TemplateShiftIds(template);
        var operations = new List<BatchShiftOperation>(dates.Count * employeeIds.Count * shiftIds.Count);
        foreach (var empId in employeeIds)
        {
            foreach (var date in dates)
            {
                foreach (var shiftId in shiftIds)
                {
                    operations.Add(new BatchShiftOperation(
                        BatchShiftOperationKind.Add,
                        empId,
                        date,
                        shiftId));
                }
            }
        }

        var batch = await _mediator.Send(
            new BatchUpdateShiftAssignmentsCommand(
                operations,
                BatchShiftConflictPolicy.ReplaceIfNoAttendance,
                SourceScheduleTemplateId: template.Id,
                AcknowledgeWeeklyOvertimeWarnings: true),
            cancellationToken);

        return new ScheduleTemplateApplyResultDto(
            template.Id,
            dates.Count,
            batch.Applied,
            batch.Skipped,
            batch.Conflicts,
            ExtractConflicts(batch.Items));
    }

    private async Task EnsureNoPatternOverlapAsync(
        IReadOnlyList<int> employeeIds,
        ScheduleTemplateEntity templateShape,
        IReadOnlyList<ShiftTemplateEntity> shifts,
        int? excludeTemplateId,
        CancellationToken cancellationToken)
    {
        // Multi-shift: chỉ chặn nếu ca mới GIAO THOA THỜI GIAN với ca đã có cùng (NV, ngày).
        // Cho phép NV ca sáng + ca chiều cùng ngày (range trùng, giờ không trùng).
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
                $"Không thể tạo: ca \"{shift.Name}\" trùng giờ với {string.Join(", ", summary)}.");
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

    private static IReadOnlyList<int> TemplateShiftIds(ScheduleTemplateEntity template)
    {
        var ids = template.Shifts
            .Select(s => s.ShiftTemplateId)
            .Distinct()
            .ToList();

        if (ids.Count > 0) return ids;
        return new[] { template.ShiftTemplateId };
    }

    private static IReadOnlyList<ScheduleTemplateApplyConflictDto> ExtractConflicts(
        IReadOnlyList<BatchShiftItemResult> items)
    {
        return items
            .Where(i => i.Status is not (BatchShiftItemStatus.Applied
                or BatchShiftItemStatus.Replaced
                or BatchShiftItemStatus.SkippedSameShift))
            .Select(i => new ScheduleTemplateApplyConflictDto(
                i.EmployeeInformationId,
                i.WorkDate,
                i.Status,
                i.Reason))
            .ToList();
    }
}
