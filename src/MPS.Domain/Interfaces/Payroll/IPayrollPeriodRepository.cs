using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

public interface IPayrollPeriodRepository
{
    /// <summary>Header (không kèm rows/cells).</summary>
    Task<PayrollPeriod?> GetByIdAsync(int id, bool asNoTracking, CancellationToken cancellationToken = default);

    /// <summary>Đầy đủ: ComplianceRefs + Rows→Cells + Notes. asNoTracking=false để edit/recompute rồi Save.</summary>
    Task<PayrollPeriod?> GetFullAsync(int id, bool asNoTracking, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PayrollPeriod>> GetAllAsync(int? year, int? month, CancellationToken cancellationToken = default);
    Task<bool> ExistsForPeriodAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>Kỳ liền trước cùng công ty (dùng diff đổi luật). Kèm ComplianceRefs.</summary>
    Task<PayrollPeriod?> GetPreviousAsync(int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Các dòng lương của 1 NV trong những kỳ đã chốt (Finalized/Locked), mới nhất trước,
    /// tối đa <paramref name="limit"/> kỳ. Kèm Period + Cells (không kèm EmployeeInformation).
    /// </summary>
    Task<IReadOnlyList<PayrollRow>> GetEmployeeRowsAsync(
        int employeeInformationId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kỳ lương với DUY NHẤT dòng của 1 NV (nếu có) — kèm ComplianceRefs, Notes, Cells, và
    /// EmployeeInformation + Position của dòng. Dùng cho self-service payslip để tái dùng
    /// PayrollSheetMapper trên payload đã được thu hẹp về 1 row (không load Total + n-1 NV khác).
    /// Trả null nếu kỳ không tồn tại. Rows sẽ rỗng nếu NV không có dòng trong kỳ.
    /// </summary>
    Task<PayrollPeriod?> GetPeriodWithEmployeeRowAsync(
        int periodId, int employeeInformationId, CancellationToken cancellationToken = default);

    /// <summary>Persist toàn bộ aggregate (rows/cells/notes/refs) trong 1 transaction.</summary>
    Task<int> AddAsync(PayrollPeriod period, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lưu thay đổi trên aggregate đã được load tracked (override cell, recompute, thêm note...).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
