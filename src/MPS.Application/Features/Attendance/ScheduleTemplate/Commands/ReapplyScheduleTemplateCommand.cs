using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

public sealed record ReapplyScheduleTemplateCommand(
    int Id,
    bool AcknowledgeWeeklyOvertimeWarnings = false,
    bool IncludeRestDaysAndHolidays = false) : IRequest<ScheduleTemplateApplyResultDto>;
