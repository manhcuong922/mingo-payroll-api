using MPS.Application.DTOs.Attendance;

namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Parse file Excel import ngày lễ. Layout đơn sheet:
///   Row 1 = header (Tên ngày lễ | Từ ngày | Đến ngày | Mô tả).
///   Row 2..N = dữ liệu, mỗi row = 1 ngày lễ.
/// Không đụng DB — chỉ trả dữ liệu thô + parse errors từng row.
/// </summary>
public interface IPublicHolidayExcelParser
{
    PublicHolidayImportParseResult Parse(Stream stream);
}
