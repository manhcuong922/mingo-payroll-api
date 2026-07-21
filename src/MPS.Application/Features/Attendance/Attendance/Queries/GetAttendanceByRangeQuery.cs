using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Queries;

/// <summary>
/// Toàn bộ AttendanceRecord trong khoảng ngày — dùng cho matrix Bảng chấm công tháng.
/// FE tự group theo (empId, workDate) để render cell.
/// </summary>
public sealed record GetAttendanceByRangeQuery(DateOnly FromDate, DateOnly ToDate)
    : IRequest<IReadOnlyList<AttendanceRecordDto>>;
