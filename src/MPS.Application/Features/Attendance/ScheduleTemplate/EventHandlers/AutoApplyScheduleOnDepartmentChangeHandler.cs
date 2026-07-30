using MediatR;
using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Attendance;
using MPS.Application.Features.Attendance.EmployeeShift.Commands;
using MPS.Domain.Interfaces.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.EventHandlers;

/// <summary>
/// Xử lý <see cref="EmployeeDepartmentChangedNotification"/> — tự động gỡ ca dept cũ và
/// apply ca dept mới cho NV. Chi tiết semantics xem
/// <c>docs/plans/auto-apply-schedule-on-department-change-plan.md</c>.
///
/// <b>Cases:</b>
/// <list type="bullet">
///   <item>Fresh assign (null → X): apply template X từ today.</item>
///   <item>Change A → B: gỡ A từ tomorrow + apply B từ today.</item>
///   <item>Leave X → null (bao gồm cả Delete NV): gỡ X từ tomorrow.</item>
/// </list>
///
/// <b>Error isolation:</b> mọi exception bị bọc trong try/catch, log ra ILogger — không throw
/// ngược làm fail flow HR update/create NV. Nếu apply fail (vượt cap, trùng ca, v.v.) → NV
/// thiếu ca, HR phải mở màn Phân ca Save/Reapply thủ công để backfill.
/// </summary>
public class AutoApplyScheduleOnDepartmentChangeHandler
    : INotificationHandler<EmployeeDepartmentChangedNotification>
{
    private readonly IScheduleTemplateRepository _templateRepo;
    private readonly IEmployeeShiftAssignmentRepository _assignmentRepo;
    private readonly IScheduleTemplateExpander _expander;
    private readonly IMediator _mediator;
    private readonly IClock _clock;
    private readonly ILogger<AutoApplyScheduleOnDepartmentChangeHandler> _logger;

    public AutoApplyScheduleOnDepartmentChangeHandler(
        IScheduleTemplateRepository templateRepo,
        IEmployeeShiftAssignmentRepository assignmentRepo,
        IScheduleTemplateExpander expander,
        IMediator mediator,
        IClock clock,
        ILogger<AutoApplyScheduleOnDepartmentChangeHandler> logger)
    {
        _templateRepo = templateRepo;
        _assignmentRepo = assignmentRepo;
        _expander = expander;
        _mediator = mediator;
        _clock = clock;
        _logger = logger;
    }

    public async Task Handle(EmployeeDepartmentChangedNotification notification, CancellationToken cancellationToken)
    {
        if (notification.OldDepartmentId == notification.NewDepartmentId) return;

        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var tomorrow = today.AddDays(1);

        // 1. Gỡ ca dept cũ (Leave, Change, Delete)
        if (notification.OldDepartmentId is int oldDept)
        {
            try
            {
                var oldTemplates = await _templateRepo.GetActiveByDepartmentIdAsync(oldDept, tomorrow, cancellationToken);
                foreach (var template in oldTemplates)
                {
                    await _assignmentRepo.DeleteUnlockedByTemplateEmployeeFromDateAsync(
                        template.Id, notification.EmployeeInformationId, tomorrow, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AutoApplyScheduleOnDepartmentChange: failed to remove old-dept shifts. EmpId={EmpId}, OldDeptId={OldDeptId}",
                    notification.EmployeeInformationId, oldDept);
            }
        }

        // 2. Apply ca dept mới — Fresh assign HOẶC Change → B (Q2a).
        if (notification.NewDepartmentId is int newDept && newDept != notification.OldDepartmentId)
        {
            try
            {
                var newTemplates = await _templateRepo.GetActiveByDepartmentIdAsync(newDept, today, cancellationToken);
                foreach (var template in newTemplates)
                {
                    await TryApplyAsync(template, notification.EmployeeInformationId, today, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AutoApplyScheduleOnDepartmentChange: failed to enumerate new-dept templates. EmpId={EmpId}, NewDeptId={NewDeptId}",
                    notification.EmployeeInformationId, newDept);
            }
        }
    }

    private async Task TryApplyAsync(
        Domain.Entities.Attendance.ScheduleTemplate template,
        int employeeInformationId,
        DateOnly effectiveFrom,
        CancellationToken cancellationToken)
    {
        try
        {
            var allDates = _expander.ExpandDates(template);
            var dates = allDates.Where(d => d >= effectiveFrom).ToList();
            if (dates.Count == 0) return;

            var shiftIds = ResolveShiftIds(template);
            if (shiftIds.Count == 0) return;

            var operations = new List<BatchShiftOperation>(dates.Count * shiftIds.Count);
            foreach (var date in dates)
            {
                foreach (var shiftId in shiftIds)
                {
                    operations.Add(new BatchShiftOperation(
                        BatchShiftOperationKind.Add, employeeInformationId, date, shiftId));
                }
            }

            await _mediator.Send(new BatchUpdateShiftAssignmentsCommand(
                operations,
                BatchShiftConflictPolicy.Skip,
                SourceScheduleTemplateId: template.Id,
                AcknowledgeWeeklyOvertimeWarnings: false,
                IncludeRestDaysAndHolidays: template.IncludeRestDaysAndHolidays),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Không throw ngược — HR flow phải success. NV thiếu ca → HR mở Phân ca Save/Reapply thủ công.
            _logger.LogWarning(ex,
                "AutoApplyScheduleOnDepartmentChange: apply template failed. TemplateId={TemplateId}, EmpId={EmpId}",
                template.Id, employeeInformationId);
        }
    }

    private static IReadOnlyList<int> ResolveShiftIds(Domain.Entities.Attendance.ScheduleTemplate template)
    {
        var ids = template.Shifts?.Select(s => s.ShiftTemplateId).ToList() ?? new List<int>();
        if (ids.Count == 0 && template.ShiftTemplateId > 0)
        {
            ids.Add(template.ShiftTemplateId);
        }
        return ids.Where(id => id > 0).Distinct().ToList();
    }
}
