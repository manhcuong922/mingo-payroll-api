using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.LeaveBalance.Queries;

/// <summary><see cref="Year"/> = null ⇒ trả quỹ phép MỌI năm của NV (dùng khi client không lọc năm).</summary>
public sealed record GetLeaveBalancesQuery(int EmployeeInformationId, int? Year)
    : IRequest<IReadOnlyList<LeaveBalanceDto>>;
