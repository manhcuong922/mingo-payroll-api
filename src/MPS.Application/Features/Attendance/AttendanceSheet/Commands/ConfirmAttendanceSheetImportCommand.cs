using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Commands;

/// <summary>
/// Bước 2/2 import bảng chấm công: apply diff đã cache ở Preview.
/// Policy hybrid: overwrite khi cell có value (đã filter ở Preview), skip locked (re-check).
/// Transaction 1-shot.
/// </summary>
public sealed record ConfirmAttendanceSheetImportCommand(
    string PreviewId) : IRequest<AttendanceSheetImportConfirmResultDto>;
