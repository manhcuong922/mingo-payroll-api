using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public class ReapplyScheduleTemplateCommandHandler
    : IRequestHandler<ReapplyScheduleTemplateCommand, ScheduleTemplateApplyResultDto>
{
    private readonly IScheduleTemplateRepository _repository;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IMediator _mediator;

    public ReapplyScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IScheduleTemplateExpander expander,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        IContractRepository contractRepository,
        IMediator mediator)
    {
        _repository = repository;
        _expander = expander;
        _assignmentRepository = assignmentRepository;
        _contractRepository = contractRepository;
        _mediator = mediator;
    }

    public async Task<ScheduleTemplateApplyResultDto> Handle(
        ReapplyScheduleTemplateCommand request,
        CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException($"ScheduleTemplate {request.Id} not found.");

        if (!template.IsActive)
        {
            throw new DomainValidationException("Bảng phân ca không active, không thể áp dụng lại.");
        }

        var dates = _expander.ExpandDates(template);
        var employeeIds = template.Employees.Select(e => e.EmployeeInformationId).Distinct().ToList();
        var shiftIds = TemplateShiftIds(template);

        // Sweep stale assignments before early-returning. If HR clears all employees
        // from a template, every assignment owned by that template should be withdrawn
        // unless it is already locked by an attendance record.
        await _assignmentRepository.DeleteStaleByScheduleTemplateAsync(
            template.Id, dates, shiftIds, employeeIds, cancellationToken);

        if (dates.Count == 0 || employeeIds.Count == 0 || shiftIds.Count == 0)
        {
            return new ScheduleTemplateApplyResultDto(
                template.Id, dates.Count, 0, 0, 0, Array.Empty<ScheduleTemplateApplyConflictDto>());
        }

        await EnsureEmployeesHaveActiveContractsAsync(employeeIds, cancellationToken);

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
                AcknowledgeWeeklyOvertimeWarnings: request.AcknowledgeWeeklyOvertimeWarnings,
                IncludeRestDaysAndHolidays: request.IncludeRestDaysAndHolidays),
            cancellationToken);

        return new ScheduleTemplateApplyResultDto(
            template.Id,
            dates.Count,
            batch.Applied,
            batch.Skipped,
            batch.Conflicts,
            batch.Items
                .Where(i => i.Status is not (BatchShiftItemStatus.Applied
                    or BatchShiftItemStatus.Replaced
                    or BatchShiftItemStatus.SkippedSameShift))
                .Select(i => new ScheduleTemplateApplyConflictDto(
                    i.EmployeeInformationId,
                    i.WorkDate,
                    i.Status,
                    i.Reason))
                .ToList());
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
            $"Không thể áp dụng lại bảng phân ca: nhân viên {preview}{suffix} chưa có hợp đồng active.");
    }

    private static IReadOnlyList<int> TemplateShiftIds(MPS.Domain.Entities.Attendance.ScheduleTemplate template)
    {
        var ids = template.Shifts
            .Select(s => s.ShiftTemplateId)
            .Distinct()
            .ToList();

        if (ids.Count > 0) return ids;
        return new[] { template.ShiftTemplateId };
    }
}
