namespace MPS.Application.Common.Payroll.Excel;

/// <summary>
/// Toạ độ cố định của sheet lương — clone bố cục file mẫu (xem DAC_TA_BANG_LUONG.md §2).
/// Lưu ý: nhóm cột cha nằm ở hàng 8, DƯỚI nhãn cột con hàng 7 — đúng như file gốc.
/// </summary>
public static class PayrollExcelLayout
{
    public const string SheetSalary = "4. LUONG";
    public const string SheetParams = "THAM SO";
    public const string SheetNotes = "GHI CHU";

    public const int RowTitle = 1;
    public const int RowSubTitle = 2;
    public const int RowPeriod = 3;
    public const int RowTotal = 6;
    public const int RowColumnLabel = 7;
    public const int RowGroupLabel = 8;
    public const int RowRateParam = 9;
    public const int RowCapParam = 10;
    public const int RowColumnNumber = 11;
    public const int RowFieldName = 12;
    public const int RowFirstData = 13;

    public const string TotalRowLabel = "Cộng";

    /// <summary>Sheet THAM SO: mỗi hằng số 1 dòng, bắt đầu từ đây (cột C = giá trị).</summary>
    public const int RowFirstConstant = 3;

    /// <summary>Cột A = STT; các cột dữ liệu bắt đầu từ B (mã NV) như file mẫu.</summary>
    public const int FirstDataColumnIndex = 2;

    /// <summary>1 → "A", 27 → "AA".</summary>
    public static string ColumnLetter(int index)
    {
        if (index < 1) throw new ArgumentOutOfRangeException(nameof(index));
        var letters = string.Empty;
        while (index > 0)
        {
            var rem = (index - 1) % 26;
            letters = (char)('A' + rem) + letters;
            index = (index - 1) / 26;
        }
        return letters;
    }
}
