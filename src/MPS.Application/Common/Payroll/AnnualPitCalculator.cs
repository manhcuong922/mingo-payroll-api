using MPS.Application.Common.Models.Compliance;

namespace MPS.Application.Common.Payroll;

/// <summary>
/// Tính thuế TNCN luỹ tiến từng phần theo BIỂU NĂM cho chỉ tiêu [37] của tờ khai 02/QTT-TNCN.
///
/// <para>Biểu thuế lưu trong hệ thống là biểu THÁNG (Phụ lục 01/PL-TNCN); quyết toán năm dùng
/// đúng thuế suất đó nhưng ngưỡng mỗi bậc nhân 12. Vì vậy hàm này nhận biểu tháng và tự quy đổi,
/// tránh phải lưu thêm một bộ biểu năm dễ lệch với biểu tháng.</para>
///
/// <para>⚠️ Kết quả KHÁC tổng thuế đã khấu trừ 12 tháng (chỉ tiêu [39]) khi thu nhập không đều
/// giữa các tháng — chênh lệch đó chính là số NLĐ được hoàn hoặc phải nộp thêm. Không được lấy
/// [39] điền cho [37].</para>
/// </summary>
public static class AnnualPitCalculator
{
    /// <summary>Số tháng dùng để quy đổi ngưỡng bậc từ biểu tháng sang biểu kỳ quyết toán.</summary>
    public const int MonthsPerYear = 12;

    /// <param name="taxableIncome">Thu nhập tính thuế cả kỳ (chỉ tiêu [36]).</param>
    /// <param name="monthlyBrackets">Biểu luỹ tiến THÁNG, thường lấy từ snapshot của kỳ lương.</param>
    /// <param name="months">
    /// Số tháng của kỳ quyết toán, mặc định 12. Cho phép truyền khác để đối chiếu với cách
    /// khấu trừ hằng tháng, hoặc cho kỳ quyết toán vắt năm ngắn hơn 12 tháng.
    /// </param>
    public static decimal Compute(
        decimal taxableIncome,
        IReadOnlyList<PitBracketSnapshot> monthlyBrackets,
        int months = MonthsPerYear)
    {
        if (monthlyBrackets is null || monthlyBrackets.Count == 0)
        {
            throw new InvalidOperationException(
                "Kỳ lương không có snapshot biểu thuế TNCN — không tính lại được thuế năm. " +
                "Kiểm tra lại dữ liệu Compliance của các kỳ trong năm.");
        }

        if (months <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(months), months, "Số tháng của kỳ quyết toán phải lớn hơn 0.");
        }

        if (taxableIncome <= 0) return 0m;

        var tax = 0m;
        foreach (var bracket in monthlyBrackets.OrderBy(b => b.Order))
        {
            var lower = bracket.LowerBound * months;
            if (taxableIncome <= lower) break;

            var upper = bracket.UpperBound.HasValue
                ? bracket.UpperBound.Value * months
                : taxableIncome;

            var segment = Math.Min(taxableIncome, upper) - lower;
            if (segment <= 0) continue;

            tax += segment * bracket.Rate;
        }

        return tax;
    }
}
