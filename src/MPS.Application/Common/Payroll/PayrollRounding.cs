using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Common.Payroll;

/// <summary>Áp quy tắc làm tròn cho kết quả cột (thuế ROUND(,3), thực nhận ROUND(,-3)).</summary>
public static class PayrollRounding
{
    public static decimal Apply(decimal value, RoundingMode mode) => mode switch
    {
        RoundingMode.ToThreeDecimals => Math.Round(value, 3, MidpointRounding.AwayFromZero),
        RoundingMode.ToThousand => Math.Round(value / 1000m, 0, MidpointRounding.AwayFromZero) * 1000m,
        _ => value,
    };

    /// <summary>
    /// Làm tròn cho 1 cột: áp <paramref name="mode"/>; nếu không có rounding tường minh mà là cột
    /// tiền thì làm tròn về đồng nguyên (VND không có phần lẻ) — khử dư số học từ phép chia công.
    /// </summary>
    public static decimal ApplyForColumn(decimal value, RoundingMode mode, ColumnDataType dataType)
    {
        var v = Apply(value, mode);
        if (mode == RoundingMode.None && dataType == ColumnDataType.Money)
            v = Math.Round(v, 0, MidpointRounding.AwayFromZero);
        return v;
    }
}
