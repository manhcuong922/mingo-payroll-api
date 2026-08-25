using Fk = MPS.Application.Common.Payroll.PayrollFieldKeys;

namespace MPS.Application.Common.Payroll.Excel;

/// <summary>
/// Vị trí + định dạng 1 hằng số trên sheet lương. <see cref="IsRateRow"/> quyết định hàng (9 = tỷ lệ/
/// đơn giá, 10 = trần/giảm trừ); <see cref="IsPercent"/> quyết định định dạng ô — hai thứ khác nhau:
/// giá cơm 40.000 nằm hàng 9 nhưng là tiền, không phải phần trăm.
/// </summary>
public sealed record ConstantPlacement(string Name, bool IsRateRow, bool IsPercent);

/// <summary>
/// Các khoá field/param là HẰNG SỐ của cả kỳ (không đổi theo nhân viên) → hiển thị ở hàng 9/10 ngay
/// trên sheet lương như file mẫu ($V$9 = 40.000, $AG$10 = 29.800.000) và gán named range cho chính ô
/// đó, nên công thức đọc được: <c>=K13*GIA_COM</c> thay vì <c>=K13*$V$9</c>.
///
/// Khoá KHÔNG có ở đây (vd hệ số năng suất, số công) là dữ liệu theo từng NV → thành cột phụ trợ.
/// Phân loại theo ĐỊNH DANH khoá, không theo dữ liệu — để kỳ chỉ có 1 nhân viên không bị nhầm
/// dữ liệu-theo-NV thành hằng số.
/// </summary>
public static class PayrollExcelConstantCatalog
{
    private static readonly Dictionary<string, ConstantPlacement> Fields = new(StringComparer.Ordinal)
    {
        [Fk.BhxhDnRate] = new("TYLE_BHXH_DN", true, true),
        [Fk.BhxhNldRate] = new("TYLE_BHXH_NLD", true, true),
        [Fk.BhytDnRate] = new("TYLE_BHYT_DN", true, true),
        [Fk.BhytNldRate] = new("TYLE_BHYT_NLD", true, true),
        [Fk.BhtnDnRate] = new("TYLE_BHTN_DN", true, true),
        [Fk.BhtnNldRate] = new("TYLE_BHTN_NLD", true, true),
        [Fk.KpcdRate] = new("TYLE_KPCD", true, true),
        // Phase 7 — thuế khoán: tỷ lệ + ngưỡng khấu trừ là số của cả kỳ (theo bản luật tại ngày trả),
        // nên hiện ở hàng tham số như các tỷ lệ BH thay vì nhân bản thành cột phụ trợ mỗi NV.
        [Fk.ShortTermFlatRate] = new("TYLE_THUE_KHOAN", true, true),
        [Fk.BhxhBhytCap] = new("TRAN_BHXH_BHYT", false, false),
        [Fk.BhtnCap] = new("TRAN_BHTN", false, false),
        [Fk.PersonalDeduction] = new("GIAMTRU_BANTHAN", false, false),
        [Fk.DependentDeduction] = new("GIAMTRU_NPT", false, false),
        [Fk.MealExemptThreshold] = new("NGUONG_COM_MIENTHUE", false, false),
        [Fk.ShortTermTaxExemptIncome] = new("NGUONG_KHAUTRU_KHOAN", false, false),
    };

    public static bool TryGetField(string key, out ConstantPlacement placement) =>
        Fields.TryGetValue(key, out placement!);

    /// <summary>Đưa khoá về tên hợp lệ cho named range Excel (chữ/số/gạch dưới, không mở đầu bằng số).</summary>
    public static string Sanitize(string key)
    {
        var name = new string(key.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray());
        return name.Length == 0 || char.IsDigit(name[0]) ? "P_" + name : name;
    }
}
