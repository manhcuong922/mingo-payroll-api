namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Cờ điều khiển bảo hiểm (cột I đặc tả §3-Khối A). Lưu dạng string code trong cell để công
/// thức so sánh trực tiếp; default suy từ HĐ (SocialInsuranceFlag/HealthInsuranceFlag/...).
/// </summary>
public static class InsuranceFlags
{
    /// <summary>Đóng đầy đủ mọi loại bảo hiểm (mặc định).</summary>
    public const string Full = "x3";

    /// <summary>Không đóng phí công đoàn (vẫn đóng BH).</summary>
    public const string NoUnion = "x";

    /// <summary>Không tính bảo hiểm.</summary>
    public const string NoInsurance = "x1";

    /// <summary>Chỉ tính BHYT.</summary>
    public const string HealthOnly = "x2";

    /// <summary>Không tính bảo hiểm — trường hợp thai sản.</summary>
    public const string Maternity = "TS";

    /// <summary>Không tính cơm + thuế khoán 20% (đặc thù).</summary>
    public const string NoMealFlatTax = "xx";

    public static readonly IReadOnlyList<string> All = [Full, NoUnion, NoInsurance, HealthOnly, Maternity, NoMealFlatTax];
}
