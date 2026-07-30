using System.Globalization;

namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Định dạng số kiểu VN (dấu chấm phân nhóm nghìn) cho chuỗi giải thích cách tính.</summary>
public static class PayrollNumberFormat
{
    private static readonly NumberFormatInfo Vi = new()
    {
        NumberGroupSeparator = ".",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
    };

    public static string Format(decimal value)
    {
        // Bỏ phần thập phân thừa: 200000 -> "200.000"; 1.5 -> "1,5".
        var rounded = Math.Round(value, 4, MidpointRounding.AwayFromZero);
        return rounded.ToString("#,0.####", Vi);
    }
}
