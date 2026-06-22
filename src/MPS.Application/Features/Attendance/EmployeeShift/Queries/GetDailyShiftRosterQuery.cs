using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

public sealed record GetDailyShiftRosterQuery(DateOnly WorkDate) : IRequest<IReadOnlyList<EmployeeShiftAssignmentDto>>;
