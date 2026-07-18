using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

public sealed record GetShiftRosterQuery(
    IReadOnlyList<int> EmployeeInformationIds,
    DateOnly FromDate,
    DateOnly ToDate) : IRequest<ShiftRosterDto>;
