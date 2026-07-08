using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.EmployeeShift.Queries;

/// <summary>
/// NV xem lịch phân ca kế hoạch của chính mình — bổ sung cho AttendanceRecord (đã chấm công).
/// </summary>
public sealed record GetMyShiftRosterQuery(
    DateOnly FromDate,
    DateOnly ToDate) : IRequest<ShiftRosterDto>;
