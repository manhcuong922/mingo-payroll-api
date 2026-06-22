using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.LeaveType.Queries;

public sealed record GetLeaveTypesQuery(
    bool? IsActive,
    string? Search,
    int Page = 1,
    int Size = 50) : IRequest<PagedResult<LeaveTypeDto>>;
