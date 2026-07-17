namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Ngữ cảnh tính 1 ô: resolve cột khác (đã tính theo topo order), field module, tham số công ty,
/// và ngữ cảnh hàm. Mỗi dòng nhân viên có 1 context.
/// </summary>
public sealed class FormulaEvaluationContext
{
    public required IReadOnlyDictionary<string, ResolvedValue> Columns { get; init; }
    public required IReadOnlyDictionary<string, ResolvedValue> Fields { get; init; }
    public required PayrollFunctionContext FunctionContext { get; init; }
}
