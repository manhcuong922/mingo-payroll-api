using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceRule.Queries;

public sealed record GetAttendanceRuleByIdQuery(int Id) : IRequest<AttendanceRuleDto?>;
