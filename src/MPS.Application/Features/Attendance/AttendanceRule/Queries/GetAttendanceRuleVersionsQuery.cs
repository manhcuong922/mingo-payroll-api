using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public sealed record GetAttendanceRuleVersionsQuery(int RuleId) : IRequest<IReadOnlyList<AttendanceRuleVersionDto>?>;
