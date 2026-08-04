using MediatR;
using MPS.Application.Common.Interfaces.Attendance;
using MPS.Application.Features.Attendance.AttendanceSheet.Common;

namespace MPS.Application.Features.Attendance.AttendanceSheet.Queries;

public sealed record AttendanceSheetExportFileDto(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Xuất "Bảng chấm công theo kỳ" .xlsx theo layout chuẩn VN — 4-row/NV (dynamic theo ca đã phân),
/// merged cells cột A-D, freeze pane F8, chú thích ký hiệu ở cuối sheet.
/// </summary>
public sealed record ExportAttendanceSheetQuery(
    DateOnly FromDate,
    DateOnly ToDate,
    int? DepartmentId) : IRequest<AttendanceSheetExportFileDto>;

public sealed class ExportAttendanceSheetQueryHandler
    : IRequestHandler<ExportAttendanceSheetQuery, AttendanceSheetExportFileDto>
{
    public const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly AttendanceSheetDataCollector _collector;
    private readonly IAttendanceSheetExcelExporter _exporter;

    public ExportAttendanceSheetQueryHandler(
        AttendanceSheetDataCollector collector,
        IAttendanceSheetExcelExporter exporter)
    {
        _collector = collector;
        _exporter = exporter;
    }

    public async Task<AttendanceSheetExportFileDto> Handle(
        ExportAttendanceSheetQuery request, CancellationToken ct)
    {
        if (request.FromDate > request.ToDate)
            throw new ArgumentException("fromDate must be <= toDate", nameof(request));

        var span = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;
        if (span > 90)
            throw new ArgumentException("Range không được vượt quá 90 ngày", nameof(request));

        var data = await _collector.CollectAsync(request.FromDate, request.ToDate, request.DepartmentId, ct);
        var content = _exporter.Build(data);
        var fileName = $"bang-cham-cong-{request.FromDate:yyyy-MM-dd}-{request.ToDate:yyyy-MM-dd}.xlsx";
        return new AttendanceSheetExportFileDto(content, fileName, XlsxContentType);
    }
}
