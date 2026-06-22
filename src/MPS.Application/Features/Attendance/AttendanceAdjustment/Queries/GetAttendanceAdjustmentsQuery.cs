using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;

public sealed record GetAttendanceAdjustmentsQuery(
    int? EmployeeInformationId,
    AdjustmentStatus? Status,
    int Page,
    int Size) : IRequest<PagedResult<AttendanceAdjustmentDto>>;
