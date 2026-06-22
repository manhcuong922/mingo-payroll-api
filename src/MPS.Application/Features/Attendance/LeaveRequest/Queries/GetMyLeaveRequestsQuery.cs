using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public sealed record GetMyLeaveRequestsQuery(
    LeaveRequestStatus? Status,
    int Page,
    int Size) : IRequest<PagedResult<LeaveRequestDto>>;
