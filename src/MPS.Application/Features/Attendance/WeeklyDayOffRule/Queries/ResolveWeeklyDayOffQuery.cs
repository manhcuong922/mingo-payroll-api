using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Queries;

public sealed record ResolveWeeklyDayOffQuery(
    int EmployeeInformationId,
    DateOnly Date) : IRequest<WeeklyDayOffResolveDto>;
