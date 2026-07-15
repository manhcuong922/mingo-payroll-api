using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.LateEarlyRequest.Queries;

public sealed record GetLateEarlyRequestsQuery(
    int? EmployeeInformationId,
    LateEarlyRequestStatus? Status,
    LateEarlyKind? Kind,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int Size = 20) : IRequest<PagedResult<LateEarlyRequestDto>>;
