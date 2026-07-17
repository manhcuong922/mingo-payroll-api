namespace MPS.Domain.Constants;

/// <summary>
/// Tên các hàm dựng sẵn mà formula engine hỗ trợ (trình dựng cho chọn như block). Logic phức
/// tạp (thuế lũy tiến, BH trần, phân bổ thu nhập theo công) đóng gói thành hàm để kế toán khỏi
/// ghép IF lồng nhiều tầng.
/// </summary>
public static class PayrollFunctions
{
    /// <summary>THUE_LUYTIEN(tntt) — thuế TNCN theo biểu lũy tiến từng phần của kỳ.</summary>
    public const string ProgressiveTax = "THUE_LUYTIEN";

    /// <summary>BH_CO_TRAN(base, rate, cap) = IF(base &gt; cap, cap, base) × rate.</summary>
    public const string InsuranceWithCap = "BH_CO_TRAN";

    /// <summary>THUNHAP_PHANBO(...) — thu nhập phân bổ theo 3 loại công (cột Y đặc tả).</summary>
    public const string IncomeProration = "THUNHAP_PHANBO";

    public const string Min = "MIN";
    public const string Max = "MAX";
    public const string Round = "ROUND";
    public const string Sum = "SUM";
    public const string Abs = "ABS";

    public static readonly IReadOnlyList<string> All =
        [ProgressiveTax, InsuranceWithCap, IncomeProration, Min, Max, Round, Sum, Abs];
}
