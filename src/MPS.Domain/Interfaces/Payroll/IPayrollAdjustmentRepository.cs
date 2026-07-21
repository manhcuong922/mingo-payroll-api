using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

/// <summary>BS-5 — đọc/ghi các khoản truy lĩnh/truy thu chờ áp vào kỳ lương mới.</summary>
public interface IPayrollAdjustmentRepository
{
    /// <summary>
    /// Các khoản CHƯA áp (<c>AppliedPeriodId = null</c>) của tập NV, sắp theo thời điểm tạo
    /// (khoản cũ được áp trước — quan trọng khi truy thu phải chia nhiều kỳ).
    /// </summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetPendingForEmployeesAsync(
        IReadOnlyCollection<int> employeeInformationIds, CancellationToken ct = default);

    /// <summary>Các khoản đã áp vào 1 kỳ — dùng khi xoá kỳ để trả chúng về trạng thái chưa áp.</summary>
    Task<IReadOnlyList<PayrollAdjustment>> GetAppliedToPeriodAsync(int periodId, CancellationToken ct = default);

    void Add(PayrollAdjustment adjustment);

    Task SaveChangesAsync(CancellationToken ct = default);
}
