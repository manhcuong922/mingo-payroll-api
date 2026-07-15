using MPS.Domain.Constants;

namespace MPS.Application.Common.Payroll.Formula;

file static class Num
{
    public static string F(decimal v) => PayrollNumberFormat.Format(v);
    public static string Pct(decimal fraction) => PayrollNumberFormat.Format(fraction * 100m) + "%";
}

/// <summary>MIN(a, b, ...) — nhỏ nhất.</summary>
public sealed class MinFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.Min;
    public int? FixedArity => null;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        if (args.Count == 0) return new(0m, "MIN() = 0");
        var v = args.Min();
        return new(v, $"MIN({string.Join(", ", args.Select(Num.F))}) = {Num.F(v)}");
    }
}

/// <summary>MAX(a, b, ...) — lớn nhất.</summary>
public sealed class MaxFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.Max;
    public int? FixedArity => null;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        if (args.Count == 0) return new(0m, "MAX() = 0");
        var v = args.Max();
        return new(v, $"MAX({string.Join(", ", args.Select(Num.F))}) = {Num.F(v)}");
    }
}

/// <summary>SUM(a, b, ...) — tổng.</summary>
public sealed class SumFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.Sum;
    public int? FixedArity => null;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        var v = args.Sum();
        return new(v, $"Tổng = {Num.F(v)}");
    }
}

/// <summary>ABS(x).</summary>
public sealed class AbsFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.Abs;
    public int? FixedArity => 1;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
        => new(Math.Abs(args[0]), $"|{Num.F(args[0])}| = {Num.F(Math.Abs(args[0]))}");
}

/// <summary>ROUND(x, digits) — làm tròn (digits âm = đến 10^-digits, vd -3 = đến nghìn). AwayFromZero như Excel.</summary>
public sealed class RoundFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.Round;
    public int? FixedArity => 2;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        var x = args[0];
        var digits = (int)args[1];
        decimal result;
        if (digits >= 0)
        {
            result = Math.Round(x, Math.Min(digits, 28), MidpointRounding.AwayFromZero);
        }
        else
        {
            var scale = (decimal)Math.Pow(10, -digits);
            result = Math.Round(x / scale, 0, MidpointRounding.AwayFromZero) * scale;
        }
        return new(result, $"Làm tròn {Num.F(x)} → {Num.F(result)}");
    }
}

/// <summary>BH_CO_TRAN(base, rate, cap) = MIN(base, cap) × rate. rate là phân số (0.08 = 8%).</summary>
public sealed class InsuranceWithCapFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.InsuranceWithCap;
    public int? FixedArity => 3;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        var @base = args[0];
        var rate = args[1];
        var cap = args[2];
        var applied = cap > 0 && @base > cap ? cap : @base;
        var value = applied * rate;
        var note = applied == cap && cap > 0 && @base > cap
            ? $"chạm trần {Num.F(cap)}"
            : $"mức đóng {Num.F(applied)}";
        return new(value, $"{note} × {Num.Pct(rate)} = {Num.F(value)}");
    }
}

/// <summary>THUE_LUYTIEN(tntt) — thuế TNCN lũy tiến từng phần theo biểu thuế của kỳ.</summary>
public sealed class ProgressiveTaxFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.ProgressiveTax;
    public int? FixedArity => 1;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        var income = args[0];
        if (income <= 0) return new(0m, "TNTT ≤ 0 → thuế 0");

        decimal tax = 0m;
        var pieces = new List<string>();
        foreach (var b in ctx.PitBrackets.OrderBy(b => b.Order))
        {
            if (income <= b.LowerBound) break;
            var upper = b.UpperBound ?? income;
            var segment = Math.Min(income, upper) - b.LowerBound;
            if (segment <= 0) continue;
            var part = segment * b.Rate;
            tax += part;
            pieces.Add($"{Num.F(segment)}×{Num.Pct(b.Rate)}");
        }

        var expr = pieces.Count > 0
            ? $"{string.Join(" + ", pieces)} = {Num.F(tax)}"
            : $"thuế 0";
        return new(tax, $"TNTT {Num.F(income)}: {expr}");
    }
}

/// <summary>
/// THUNHAP_PHANBO(baseSum, phuCapLuong, troCapThuViec, hocViec, congChuan, congChinhThuc,
/// congThuViec, congHocViec, phuCapNgoaiCong) — thu nhập phân bổ theo 3 loại công (cột Y đặc tả).
/// = (baseSum−Q−R)/J×M + (baseSum−P−R)/J×L + (R/J)×N + phuCapNgoaiCong.
/// </summary>
public sealed class IncomeProrationFunction : IPayrollFunction
{
    public string Name => PayrollFunctions.IncomeProration;
    public int? FixedArity => 9;
    public FunctionResult Invoke(IReadOnlyList<decimal> args, PayrollFunctionContext ctx)
    {
        var baseSum = args[0];
        var p = args[1];  // phụ cấp lương
        var q = args[2];  // trợ cấp thử việc
        var r = args[3];  // học việc
        var j = args[4];  // công chuẩn
        var m = args[5];  // công chính thức
        var l = args[6];  // công thử việc
        var n = args[7];  // công học việc
        var phuCapNgoai = args[8];

        decimal term1 = 0m, term2 = 0m, term3 = 0m;
        if (j != 0)
        {
            term1 = (baseSum - q - r) / j * m;
            term2 = (baseSum - p - r) / j * l;
            term3 = r / j * n;
        }
        var total = term1 + term2 + term3 + phuCapNgoai;

        var expr =
            $"Chính thức {Num.F(m)} công: {Num.F(term1)} + " +
            $"Thử việc {Num.F(l)} công: {Num.F(term2)} + " +
            $"Học việc {Num.F(n)} công: {Num.F(term3)} + " +
            $"Phụ cấp: {Num.F(phuCapNgoai)} = {Num.F(total)}";
        return new(total, expr);
    }
}
