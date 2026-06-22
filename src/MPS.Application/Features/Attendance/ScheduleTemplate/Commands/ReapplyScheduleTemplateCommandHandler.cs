using MediatR;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public class ReapplyScheduleTemplateCommandHandler
    : IRequestHandler<ReapplyScheduleTemplateCommand, ScheduleTemplateApplyResultDto>
{
    private readonly IScheduleTemplateRepository _repository;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepository;
    private readonly IMediator _mediator;

    public ReapplyScheduleTemplateCommandHandler(
        IScheduleTemplateRepository repository,
        IScheduleTemplateExpander expander,
        IEmployeeShiftAssignmentRepository assignmentRepository,
        IMediator mediator)
    {
        _repository = repository;
        _expander = expander;
        _assignmentRepository = assignmentRepository;
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

        if (dates.Count == 0 || employeeIds.Count == 0)
        {
            return new ScheduleTemplateApplyResultDto(
                template.Id, dates.Count, 0, 0, 0, Array.Empty<ScheduleTemplateApplyConflictDto>());
        }

        // Sweep stale assignments: ngày nào đã bị bỏ khỏi pattern mới (VD: bỏ T7)
        // cần xóa trước khi apply, tránh false-conflict khi tạo template khác.
        await _assignmentRepository.DeleteStaleByScheduleTemplateAsync(
            template.Id, dates, cancellationToken);

        var operations = new List<BatchShiftOperation>(dates.Count * employeeIds.Count);
        foreach (var empId in employeeIds)
        {
            foreach (var date in dates)
            {
                operations.Add(new BatchShiftOperation(
                    BatchShiftOperationKind.Add,
                    empId,
                    date,
                    template.ShiftTemplateId));
            }
        }

        var batch = await _mediator.Send(
            new BatchUpdateShiftAssignmentsCommand(
                operations,
                BatchShiftConflictPolicy.ReplaceIfNoAttendance,
                SourceScheduleTemplateId: template.Id),
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
}
