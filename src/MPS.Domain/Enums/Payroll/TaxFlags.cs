namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Cờ cách tính thuế TNCN (cột AY đặc tả §3-Khối G). Default suy từ Contract.TaxPolicy
/// (ProgressivePit→Progressive, FlatRate10Percent→Khoan10, Exempt→CamKet).
/// </summary>
public static class TaxFlags
{
    /// <summary>Tính theo biểu lũy tiến từng phần (mặc định).</summary>
    public const string Progressive = "";

    /// <summary>Thuế khoán 10% trên tổng thu nhập.</summary>
    public const string Khoan10 = "x";

    /// <summary>Thuế khoán 20% trên tổng thu nhập.</summary>
    public const string Khoan20 = "xx";

    /// <summary>Cam kết (thu nhập thấp) — không khấu trừ thuế.</summary>
    public const string CamKet = "CamKet";

    public static readonly IReadOnlyList<string> All = [Progressive, Khoan10, Khoan20, CamKet];
}
