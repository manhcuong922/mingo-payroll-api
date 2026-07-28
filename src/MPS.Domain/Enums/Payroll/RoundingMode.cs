namespace MPS.Domain.Enums.Payroll;

/// <summary>
/// Quy tắc làm tròn áp cho kết quả cột (theo đặc tả): thuế TNCN <c>ROUND(x,3)</c>,
/// thực nhận <c>ROUND(x,-3)</c> (đến nghìn đồng).
/// </summary>
public enum RoundingMode
{
    None = 0,
    ToThreeDecimals = 1, // ROUND(x, 3)
    ToThousand = 2,      // ROUND(x, -3) — làm tròn đến nghìn
}
