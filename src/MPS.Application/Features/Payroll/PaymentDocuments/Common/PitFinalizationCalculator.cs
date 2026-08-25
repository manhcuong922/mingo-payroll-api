using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Features.Payroll.PaymentDocuments.Common;

/// <summary>Giá trị các chỉ tiêu của tờ khai 02/QTT-TNCN, key là số chỉ tiêu (19..57).</summary>
public sealed record PitFinalizationFigures(IReadOnlyDictionary<int, decimal?> Indicators)
{
    public decimal? this[int indicator]
        => Indicators.TryGetValue(indicator, out var value) ? value : null;
}

/// <summary>
/// Tính các chỉ tiêu tờ khai quyết toán thuế TNCN năm từ những kỳ lương đã chốt trong năm.
///
/// <para>Điểm cốt lõi: chỉ tiêu [37] KHÔNG phải tổng thuế đã khấu trừ 12 tháng ([39]) mà phải
/// tính lại trên biểu thuế NĂM. Hai số này lệch nhau khi thu nhập không đều giữa các tháng, và
/// chênh lệch chính là số NLĐ được hoàn hoặc phải nộp thêm.</para>
///
/// <para>Những chỉ tiêu doanh nghiệp KHÔNG THỂ biết (thu nhập nơi khác, từ thiện, chi khám chữa
/// bệnh / giáo dục, thuế nộp ở nước ngoài, lựa chọn hoàn thuế của NLĐ) được để null ⇒ ô trống
/// trên tờ khai, để NLĐ tự khai. Điền bừa 0 vào đó là khai sai thay người lao động.</para>
/// </summary>
public static class PitFinalizationCalculator
{
    /// <summary>Ngưỡng miễn thuế sau quyết toán (chỉ tiêu [52]).</summary>
    public const decimal SmallTaxWaiverThreshold = 50_000m;

    /// <param name="profileDependentsCount">
    /// Số người phụ thuộc trên hồ sơ hiện tại — chỉ dùng khi kỳ lương không có cột SL_NPT.
    /// </param>
    public static PitFinalizationFigures Compute(
        IReadOnlyList<PayrollRow> yearRows,
        IReadOnlyList<PitBracketSnapshot> monthlyBrackets,
        int? profileDependentsCount)
    {
        var taxableIncome = PayrollCellReader.SumOrZero(yearRows, PayrollColumnCodes.Tnct);
        var exemptIncome = SumColumns(yearRows, PitFinalizationColumns.ExemptIncomeToDeclare);

        var selfDeduction = PayrollCellReader.SumOrZero(yearRows, PayrollColumnCodes.GtBanThan);
        var dependentDeduction = PayrollCellReader.SumOrZero(yearRows, PayrollColumnCodes.GtNpt);
        var insuranceDeduction = PayrollCellReader.SumOrZero(yearRows, PayrollColumnCodes.CongBhNld);
        var totalDeduction = selfDeduction + dependentDeduction + insuranceDeduction;

        // [19] đã là thu nhập CHỊU thuế (engine trừ sẵn các khoản miễn), nên KHÔNG trừ [23] lần nữa
        // — trừ hai lần là khai thiếu thu nhập tính thuế.
        var assessableIncome = Math.Max(0m, taxableIncome - totalDeduction);

        var annualTax = AnnualPitCalculator.Compute(assessableIncome, monthlyBrackets);
        var withheldTax = PayrollCellReader.SumOrZero(yearRows, PayrollColumnCodes.ThueTncn);

        var stillPayable = Math.Max(0m, annualTax - withheldTax);
        var overpaid = Math.Max(0m, withheldTax - annualTax);

        var indicators = new Dictionary<int, decimal?>
        {
            [19] = taxableIncome,          // = [20] + [22]
            [20] = taxableIncome,          // toàn bộ phát sinh tại Việt Nam
            [21] = null,                   // miễn giảm theo Hiệp định — DN không nắm
            [22] = null,                   // TNCT phát sinh ngoài Việt Nam — DN không nắm

            [23] = exemptIncome,           // = [24] + [25] + [26]
            [24] = null,                   // nhiệm vụ KHCN & đổi mới sáng tạo
            [25] = null,                   // chuyên gia hỗ trợ khởi nghiệp
            [26] = exemptIncome,           // phụ trội làm thêm giờ / làm đêm

            [27] = ResolveDependentsCount(yearRows, profileDependentsCount),

            [28] = totalDeduction,         // = [29]+…+[35]
            [29] = selfDeduction,
            [30] = dependentDeduction,
            [31] = null,                   // từ thiện, nhân đạo, khuyến học
            [32] = insuranceDeduction,
            [33] = null,                   // hưu trí bổ sung / nhân thọ
            [34] = null,                   // khám chữa bệnh trong nước
            [35] = null,                   // giáo dục - đào tạo trong nước

            [36] = assessableIncome,
            [37] = annualTax,

            [38] = withheldTax,            // = [39]+[40]+[41]−[42]−[43]
            [39] = withheldTax,            // đã khấu trừ tại tổ chức trả thu nhập
            [40] = null,                   // tự nộp không qua tổ chức trả thu nhập
            [41] = null,                   // đã nộp ở nước ngoài
            [42] = null,
            [43] = null,

            [44] = null,                   // tổng được giảm
            [45] = null,
            [46] = null,
            [47] = null,
            [48] = null,                   // tổng được miễn
            [49] = null,
            [50] = null,

            [51] = stillPayable,
            [52] = stillPayable > 0 && stillPayable <= SmallTaxWaiverThreshold
                ? stillPayable
                : null,
            [53] = overpaid,

            [54] = null,                   // đề nghị hoàn — NLĐ tự chọn
            [55] = null,
            [56] = null,
            [57] = null,                   // bù trừ kỳ sau — NLĐ tự chọn
        };

        return new PitFinalizationFigures(indicators);
    }

    private static decimal SumColumns(IReadOnlyList<PayrollRow> rows, IEnumerable<string> columns)
        => columns.Sum(column => PayrollCellReader.SumOrZero(rows, column));

    /// <summary>
    /// Số NPT lấy theo mức CAO NHẤT đã khai trong năm trên chính bảng lương, không lấy con số
    /// trên hồ sơ hiện tại.
    ///
    /// <para>Lý do: chỉ tiêu [30] (giảm trừ cho NPT) suy từ bảng lương. NV đăng ký NPT vào tháng
    /// 12 thì hồ sơ ghi 1 người nhưng cả năm hầu như không được giảm trừ — in [27]=1 cạnh [30]=0
    /// là hai ô đá nhau ngay trên tờ khai, cán bộ thuế đối chiếu sẽ bắt.</para>
    ///
    /// <para>Chỉ rơi về số trên hồ sơ khi kỳ lương không hề có cột SL_NPT (bộ công thức cũ).</para>
    /// </summary>
    private static decimal? ResolveDependentsCount(
        IReadOnlyList<PayrollRow> rows, int? profileDependentsCount)
    {
        var claimed = rows
            .Select(row => PayrollCellReader.Value(row, PayrollColumnCodes.SlNpt))
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return claimed.Count > 0 ? claimed.Max() : profileDependentsCount;
    }
}
