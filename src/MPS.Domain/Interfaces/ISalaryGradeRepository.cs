using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface ISalaryGradeRepository
{
    Task<SalaryGrade?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryGrade>> GetAllAsync(int? positionId, bool? isActive, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken cancellationToken = default);
    Task<bool> HasContractsAsync(int id, CancellationToken cancellationToken = default);
    Task<int> AddAsync(SalaryGrade entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(SalaryGrade entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete nếu không có Contract tham chiếu; ngược lại set IsActive=false (soft).
    /// </summary>
    /// <returns>true nếu xoá/deactivate thành công; false nếu không tìm thấy.</returns>
    Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default);
}
