using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

public interface IPayrollFormulaSetRepository
{
    Task<PayrollFormulaSet?> GetByIdAsync(int id, bool includeColumns, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PayrollFormulaSet>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<PayrollFormulaSet?> GetDefaultAsync(bool includeColumns, CancellationToken cancellationToken = default);
    Task<int> AddAsync(PayrollFormulaSet entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PayrollFormulaSet entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> IsUsedByAnyPeriodAsync(int formulaSetId, CancellationToken cancellationToken = default);

    /// <summary>Đặt 1 bảng làm mặc định (bỏ cờ mặc định ở mọi bảng khác — chỉ 1 bảng IsDefault=true).</summary>
    Task<bool> SetDefaultAsync(int formulaSetId, int userId, CancellationToken cancellationToken = default);

    // ===== Column ops =====
    Task<SalaryColumnDefinition?> GetColumnAsync(int columnId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryColumnDefinition>> GetColumnsAsync(int formulaSetId, CancellationToken cancellationToken = default);
    Task<int> AddColumnAsync(SalaryColumnDefinition column, CancellationToken cancellationToken = default);
    Task<bool> UpdateColumnAsync(SalaryColumnDefinition column, CancellationToken cancellationToken = default);
    Task<bool> DeleteColumnAsync(int columnId, CancellationToken cancellationToken = default);
    Task<bool> ColumnCodeExistsAsync(int formulaSetId, string columnCode, int? excludeColumnId, CancellationToken cancellationToken = default);
}
