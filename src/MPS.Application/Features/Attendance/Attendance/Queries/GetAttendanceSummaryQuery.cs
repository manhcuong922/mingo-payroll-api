using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

public sealed record GetAttendanceSummaryQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    int? EmployeeInformationId,
    int Page,
    int Size) : IRequest<PagedResult<EmployeeAttendanceSummaryDto>>;
