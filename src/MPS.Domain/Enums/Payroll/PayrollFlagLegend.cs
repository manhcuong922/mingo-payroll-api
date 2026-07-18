namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Chú giải ý nghĩa từng mã cờ bảo hiểm (FlagBh) và cờ thuế (FlagThue). Dùng cho sheet "GHI CHU"
/// của Excel và hiển thị trên UI. Hai cột cờ do hệ thống suy từ hợp đồng — CHỈ ĐỌC, không sửa tay.
/// ⚠️ Giữ ĐỒNG BỘ nội dung với FE: mingo-payroll-ui/src/lib/payrollFlags.ts
/// </summary>
public static class PayrollFlagLegend
{
    public readonly record struct Entry(string Code, string Label, string Description);

    /// <summary>Cờ bảo hiểm (cột "Cờ BH").</summary>
    public static readonly IReadOnlyList<Entry> Insurance = new[]
    {
        new Entry(InsuranceFlags.Full, "Đóng đầy đủ",
            "Đóng đầy đủ BHXH + BHYT + BHTN và trích KPCĐ."),
        new Entry(InsuranceFlags.NoUnion, "Không KPCĐ",
            "Vẫn đóng BHXH/BHYT/BHTN nhưng KHÔNG trích kinh phí công đoàn (KPCĐ)."),
        new Entry(InsuranceFlags.HealthOnly, "Chỉ BHYT",
            "Chỉ đóng BHYT; không đóng BHXH và BHTN."),
        new Entry(InsuranceFlags.NoInsurance, "Không bảo hiểm",
            "Không đóng loại bảo hiểm nào (BHXH = BHYT = BHTN = 0)."),
        new Entry(InsuranceFlags.Maternity, "Thai sản",
            "Đang nghỉ thai sản — không đóng bảo hiểm trong kỳ."),
        new Entry(InsuranceFlags.NoMealFlatTax, "Không cơm + khoán",
            "Không tính phụ cấp cơm (thường đi kèm cờ thuế khoán 20%)."),
    };

    /// <summary>Cờ thuế TNCN (cột "Cờ thuế").</summary>
    public static readonly IReadOnlyList<Entry> Tax = new[]
    {
        new Entry(TaxFlags.Progressive, "Luỹ tiến",
            "Tính theo biểu thuế luỹ tiến từng phần; được trừ giảm trừ bản thân + người phụ thuộc."),
        new Entry(TaxFlags.Khoan10, "Khoán 10%",
            "Thuế = 10% tổng thu nhập, không giảm trừ (lao động thời vụ có mã số thuế)."),
        new Entry(TaxFlags.Khoan20, "Khoán 20%",
            "Thuế = 20% tổng thu nhập, không giảm trừ (không có mã số thuế)."),
        new Entry(TaxFlags.CamKet, "Cam kết",
            "Có cam kết thu nhập thấp — tạm không khấu trừ thuế (thuế = 0)."),
    };

    /// <summary>Mã hiển thị: cờ luỹ tiến lưu chuỗi rỗng → hiện "(trống)".</summary>
    public static string DisplayCode(string code) => string.IsNullOrEmpty(code) ? "(trống)" : code;
}
