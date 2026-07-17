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

    /// <summary>Persist toàn bộ aggregate (rows/cells/notes/refs) trong 1 transaction.</summary>
    Task<int> AddAsync(PayrollPeriod period, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Lưu thay đổi trên aggregate đã được load tracked (override cell, recompute, thêm note...).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
