using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.LeaveRequest.Queries;

public sealed record GetLeaveRequestsQuery(
    int? EmployeeInformationId,
    LeaveRequestStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int Size) : IRequest<PagedResult<LeaveRequestDto>>;
