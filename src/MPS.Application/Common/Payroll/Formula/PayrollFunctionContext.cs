using MPS.Application.Common.Models.Compliance;

namespace MPS.Application.Common.Payroll.Formula;

/// <summary>
/// Ngữ cảnh luật của kỳ mà các hàm dựng sẵn cần (vd THUE_LUYTIEN tra biểu thuế). Lấy từ snapshot
/// compliance của kỳ lương — bất biến sau khi tạo kỳ.
/// </summary>
public sealed class PayrollFunctionContext
{
    public IReadOnlyList<PitBracketSnapshot> PitBrackets { get; init; } = Array.Empty<PitBracketSnapshot>();
}
