using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.Common;

/// <summary>
/// Đọc giá trị hiệu lực của một ô trên dòng bảng lương.
///
/// <para>Quy tắc: ô bị kế toán sửa tay thì lấy <see cref="PayrollCellValue.OverrideValue"/>,
/// còn lại lấy <see cref="PayrollCellValue.ComputedValue"/>. Bỏ qua quy tắc này sẽ đọc ra số
/// engine tính chứ không phải số kế toán đã chốt — chứng từ lệch bảng lương.</para>
/// </summary>
public static class PayrollCellReader
{
    /// <summary>Giá trị hiệu lực, hoặc null khi kỳ lương không có cột đó.</summary>
    public static decimal? Value(PayrollRow row, string columnCode)
    {
        var cell = row.Cells.FirstOrDefault(c =>
            string.Equals(c.ColumnCode, columnCode, StringComparison.OrdinalIgnoreCase));

        if (cell is null) return null;

        return cell.IsOverridden ? cell.OverrideValue : cell.ComputedValue;
    }

    /// <summary>
    /// Giá trị hiệu lực, quy về 0 khi thiếu cột hoặc ô rỗng — dùng cho các phép cộng tiền
    /// mà cột vắng mặt đồng nghĩa "không phát sinh".
    /// </summary>
    public static decimal ValueOrZero(PayrollRow row, string columnCode)
        => Value(row, columnCode) ?? 0m;

    /// <summary>Tổng giá trị hiệu lực của một cột trên nhiều dòng/nhiều kỳ.</summary>
    public static decimal SumOrZero(IEnumerable<PayrollRow> rows, string columnCode)
        => rows.Sum(row => ValueOrZero(row, columnCode));
}
