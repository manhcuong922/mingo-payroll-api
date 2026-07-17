namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Kết quả 1 hàm dựng sẵn: giá trị + chuỗi giải thích (vd "Bậc 2: 8.000.000 × 10% − 250.000").</summary>
public readonly record struct FunctionResult(decimal Value, string Explanation);

/// <summary>1 hàm dựng sẵn cho formula engine (THUE_LUYTIEN, BH_CO_TRAN, MIN/MAX/ROUND/SUM...).</summary>
public interface IPayrollFunction
{
    string Name { get; }

    /// <summary>Số tham số cố định; null = biến thiên (SUM/MIN/MAX).</summary>
    int? FixedArity { get; }

    FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext context);
}
