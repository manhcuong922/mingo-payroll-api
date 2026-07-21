using MediatR;
using MPS.Domain.Enums.Attendance;

namespace MPS.Application.Features.Attendance.Attendance.Commands;

/// <summary>
/// Sửa 1 AttendanceRecord theo Id — hoạt động cho cả record ca chính và record OT
/// (OT record không có ShiftTemplateId nên endpoint composite key cũ không dùng được).
/// </summary>
public sealed record UpdateAttendanceByIdCommand(
    int Id,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    AttendanceStatus? Status,
    string? Notes,
    string? OverrideReason) : IRequest<bool>;
