using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.WeeklyDayOffRule.Queries;

public sealed record GetWeeklyDayOffRulesQuery(
    bool IncludeInactive = false,
    string? ApplyScope = null,
    DateOnly? EffectiveOn = null) : IRequest<IReadOnlyList<WeeklyDayOffRuleDto>>;
