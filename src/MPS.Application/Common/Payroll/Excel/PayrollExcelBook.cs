using MPS.Application.Common.Models.Compliance;
using MPS.Application.DTOs.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll.Excel;

/// <summary>1 cột trên sheet lương (đã gán chữ cái Excel). <see cref="FieldKey"/> ≠ null = cột phụ trợ.</summary>
public sealed record PayrollExcelColumn(
    string ColumnCode,
    string Label,
    string? GroupName,
    string Letter,
    int Index,
    ColumnDataType DataType,
    ColumnValueSource ValueSource,
    RoundingMode Rounding,
    bool IsMoney,
    string? FieldKey = null)
{
    /// <summary>Tên field kỹ thuật in ở hàng 12 (như file mẫu).</summary>
    public string FieldName => FieldKey is null ? ColumnCode : PayrollExcelConstantCatalog.Sanitize(FieldKey);
}

/// <summary>
/// 1 ô dữ liệu. <see cref="Formula"/> ≠ null thì ghi cả công thức lẫn giá trị cache: mở file thấy số
/// đúng ngay, bấm vào ô thấy công thức, sửa input thì Excel tự tính lại.
/// </summary>
public sealed record PayrollExcelCell(
    decimal? Number = null,
    string? Text = null,
    string? Formula = null,
    bool IsOverridden = false,
    string? Comment = null);

public sealed record PayrollExcelDataRow(int Ordinal, IReadOnlyList<PayrollExcelCell> Cells);

/// <summary>Hằng số của kỳ đặt ở hàng 9/10 sheet lương (hoặc sheet THAM SO nếu không có cột neo).</summary>
public sealed record PayrollExcelConstant(
    string Key,
    string Name,
    string Label,
    decimal Value,
    bool IsPercent,
    string SheetName,
    string CellRef)
{
    /// <summary>Đích của named range, dạng tuyệt đối: <c>'4. LUONG'!$V$9</c>.</summary>
    public string AbsoluteRef
    {
        get
        {
            var letters = new string(CellRef.TakeWhile(char.IsLetter).ToArray());
            var digits = new string(CellRef.SkipWhile(char.IsLetter).ToArray());
            return $"'{SheetName}'!${letters}${digits}";
        }
    }
}

/// <summary>
/// Mô hình workbook đã tính xong (thuần dữ liệu) — Infrastructure chỉ việc render bằng OpenXml.
/// Tách vậy để test được toàn bộ logic bố cục + công thức mà không cần mở file Excel.
/// </summary>
public sealed record PayrollExcelBook(
    string CompanyName,
    int PeriodYear,
    int PeriodMonth,
    string Status,
    decimal StandardWorkingDays,
    IReadOnlyList<PayrollExcelColumn> Columns,
    IReadOnlyList<PayrollExcelDataRow> Rows,
    IReadOnlyList<PayrollExcelCell> TotalCells,
    IReadOnlyList<PayrollExcelConstant> Constants,
    IReadOnlyList<PitBracketSnapshot> PitBrackets,
    IReadOnlyList<PayrollNoteDto> Notes,
    IReadOnlyList<PayrollComplianceRefDto> ComplianceRefs,
    IReadOnlyDictionary<string, string> HeaderComments)
{
    public string FileName => $"bang-luong-{PeriodMonth:00}-{PeriodYear}.xlsx";
}
