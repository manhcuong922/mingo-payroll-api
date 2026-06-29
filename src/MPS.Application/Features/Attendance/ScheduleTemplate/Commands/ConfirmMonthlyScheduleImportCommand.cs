using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.ScheduleTemplate.Commands;

/// <summary>
/// Bước 2/2 import bảng phân ca: dùng PreviewToken đã sinh ở bước preview, gộp
/// các operation theo ShiftTemplate, tạo 1 ScheduleTemplate per group (RepeatUnit=Month,
/// DaysOfMonth = các ngày trong group) rồi áp assignments qua BatchUpdateShiftAssignmentsCommand
/// với SourceScheduleTemplateId trỏ về template vừa tạo. ConflictPolicy = Fail.
/// </summary>
public sealed record ConfirmMonthlyScheduleImportCommand(
    string PreviewToken,
    bool AcknowledgeWeeklyOvertimeWarnings = false)
    : IRequest<MonthlyScheduleImportConfirmResult>;
