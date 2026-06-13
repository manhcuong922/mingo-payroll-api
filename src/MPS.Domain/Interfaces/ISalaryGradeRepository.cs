using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface ISalaryGradeRepository
{
    Task<SalaryGrade?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryGrade>> GetAllAsync(int? positionId, bool? isActive, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<SalaryGrade> Items, int TotalCount)> GetPagedAsync(
        int? positionId,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<SalaryGrade?> GetActiveByPositionAndLevelAsync(int positionId, int level, CancellationToken cancellationToken = default);
    Task<bool> HasContractsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically deactivate the currently active grade for (PositionId, Level) if any, then insert <paramref name="newGrade"/> as active.
    /// Returns the new grade's Id.
    /// </summary>
    Task<int> CreateWithDeactivationAsync(SalaryGrade newGrade, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(SalaryGrade entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete nếu không có Contract tham chiếu; ngược lại set IsActive=false (soft).
    /// </summary>
    /// <returns>true nếu xoá/deactivate thành công; false nếu không tìm thấy.</returns>
    Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default);
}
