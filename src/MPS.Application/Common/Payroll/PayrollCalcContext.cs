using MPS.Application.Common.Payroll.Formula;

namespace MPS.Application.Common.Payroll;

/// <summary>Ngữ cảnh tính 1 dòng nhân viên: field của NV đó + tham số + ngữ cảnh hàm (dùng chung kỳ).</summary>
public sealed class PayrollCalcContext
{
    public required IReadOnlyDictionary<string, ResolvedValue> Fields { get; init; }
    public required PayrollFunctionContext FunctionContext { get; init; }
}
