using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.WfhRequest.Queries;

public sealed record GetMyWfhRequestsQuery(
    WfhRequestStatus? Status,
    int Page = 1,
    int Size = 20) : IRequest<PagedResult<WfhRequestDto>>;
