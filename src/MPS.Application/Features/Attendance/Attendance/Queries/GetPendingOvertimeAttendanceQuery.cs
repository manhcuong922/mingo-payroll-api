using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

/// <summary>
/// List OT Approved cho 1 ngày mà CHƯA có AttendanceRecord — HR chấm cho các OT này.
/// </summary>
public sealed record GetPendingOvertimeAttendanceQuery(DateOnly WorkDate)
    : IRequest<IReadOnlyList<PendingOvertimeAttendanceDto>>;
