using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct);
    Task<Department?> GetByIdAsync(int id, CancellationToken ct);
    Task<Department?> GetByManagerEmployeeIdAsync(int employeeId, CancellationToken ct);
    Task<(IReadOnlyList<Department> Items, int Total)> GetPagedAsync(string? keyword, int page, int pageSize, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct);
    Task<bool> HasEmployeesAsync(int id, CancellationToken ct);
    Task<int> AddAsync(Department entity, CancellationToken ct);
    Task<bool> UpdateAsync(Department entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
