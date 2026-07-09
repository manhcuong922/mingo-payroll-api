using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.OvertimeRequest.Queries;

public sealed record GetOvertimeRequestsQuery(
    int? EmployeeInformationId,
    OtRequestStatus? Status,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page,
    int Size,
    int? CreatedByUserId = null,
    int? ProjectId = null) : IRequest<PagedResult<OvertimeRequestDto>>;
