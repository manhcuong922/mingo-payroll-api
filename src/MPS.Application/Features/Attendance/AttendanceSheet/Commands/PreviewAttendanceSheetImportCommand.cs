using MediatR;
using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Commands;

/// <summary>
/// Bước 1/2 import bảng chấm công: parse file, resolve NV/shift, so với DB, build diff.
/// KHÔNG ghi DB. Cache resolved cells vào <c>IAttendanceSheetImportCache</c> 15 phút.
/// </summary>
public sealed record PreviewAttendanceSheetImportCommand(
    Stream File,
    DateOnly FromDate,
    DateOnly ToDate) : IRequest<AttendanceSheetImportPreviewDto>;
