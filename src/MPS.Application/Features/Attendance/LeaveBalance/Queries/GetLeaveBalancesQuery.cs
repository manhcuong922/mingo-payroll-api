using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Queries;

public sealed record GetLeaveBalancesQuery(int EmployeeInformationId, int Year)
    : IRequest<IReadOnlyList<LeaveBalanceDto>>;
