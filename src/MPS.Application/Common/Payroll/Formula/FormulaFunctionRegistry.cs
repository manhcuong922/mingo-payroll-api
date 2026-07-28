namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Danh bạ hàm dựng sẵn — đăng ký singleton (stateless).</summary>
public sealed class FormulaFunctionRegistry : IFormulaFunctionRegistry
{
    private readonly Dictionary<string, IPayrollFunction> _functions;

    public FormulaFunctionRegistry()
    {
        var all = new IPayrollFunction[]
        {
            new MinFunction(),
            new MaxFunction(),
            new SumFunction(),
            new AbsFunction(),
            new RoundFunction(),
            new InsuranceWithCapFunction(),
            new ProgressiveTaxFunction(),
            new IncomeProrationFunction(),
        };
        _functions = all.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGet(string name, out IPayrollFunction function) => _functions.TryGetValue(name, out function!);

    public IReadOnlyCollection<string> Names => _functions.Keys;
}
