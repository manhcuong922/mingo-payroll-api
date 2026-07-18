using MPS.Domain.Entities.Payroll;

namespace MPS.Application.Common.Interfaces.Payroll;

/// <summary>
/// Sinh ghi chú cho kỳ lương: (1) trần/ngưỡng tĩnh từ compliance kỳ này; (2) thay đổi luật so kỳ
/// trước — từ số cũ → số mới + tên luật. Logic thuần (không DB).
/// </summary>
public interface IPayrollLawChangeNoteBuilder
{
    IReadOnlyList<PayrollNote> Build(
        IReadOnlyList<PayrollComplianceRef> currentRefs,
        IReadOnlyList<PayrollComplianceRef>? previousRefs);
}
