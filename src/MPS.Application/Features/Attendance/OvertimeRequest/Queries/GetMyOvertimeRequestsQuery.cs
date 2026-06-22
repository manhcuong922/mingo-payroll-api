using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public sealed record GetMyOvertimeRequestsQuery(
    OtRequestStatus? Status,
    int Page,
    int Size) : IRequest<PagedResult<OvertimeRequestDto>>;
