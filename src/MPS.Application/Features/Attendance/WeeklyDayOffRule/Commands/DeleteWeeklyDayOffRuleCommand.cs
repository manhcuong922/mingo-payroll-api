using MediatR;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Commands;

public sealed record DeleteWeeklyDayOffRuleCommand(int Id) : IRequest<bool>;
