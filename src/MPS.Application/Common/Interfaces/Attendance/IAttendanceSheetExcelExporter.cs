using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Common.Interfaces.Attendance;

/// <summary>
/// Render <see cref="AttendanceSheetExportDto"/> ra file .xlsx bytes.
/// Layout chuẩn VN: 4-row/NV (dynamic theo ca), merged cells, freeze F8, chú thích ký hiệu cuối sheet.
/// </summary>
public interface IAttendanceSheetExcelExporter
{
    byte[] Build(AttendanceSheetExportDto data);
}
