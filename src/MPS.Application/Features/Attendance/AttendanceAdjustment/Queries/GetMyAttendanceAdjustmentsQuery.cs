using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs.Attendance;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceAdjustment.Queries;

/// <summary>
/// NV list đơn AttendanceAdjustment của chính mình. Handler tự resolve
/// employeeInformationId từ current user, không nhận từ client.
/// </summary>
public sealed record GetMyAttendanceAdjustmentsQuery(
    AdjustmentStatus? Status,
    int Page,
    int Size) : IRequest<PagedResult<AttendanceAdjustmentDto>>;
