namespace MPS.Application.Common.Payroll.Formula;

/// <summary>Danh bạ hàm dựng sẵn — evaluator dispatch theo tên; validator kiểm tên + số tham số.</summary>
public interface IFormulaFunctionRegistry
{
    bool TryGet(string name, out IPayrollFunction function);
    IReadOnlyCollection<string> Names { get; }
}
