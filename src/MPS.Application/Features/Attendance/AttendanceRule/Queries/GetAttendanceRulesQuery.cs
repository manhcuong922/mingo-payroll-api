using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public sealed record GetAttendanceRulesQuery(
    bool? IsActive,
    int Page,
    int Size) : IRequest<PagedResult<AttendanceRuleDto>>;
