using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct);
    Task<Project?> GetByIdAsync(int id, CancellationToken ct);
    Task<Project?> GetByIdWithTasksAsync(int id, CancellationToken ct);   // include Tasks + DependsOn cho EV/PV
    Task<IReadOnlyList<Project>> GetByDepartmentAsync(int departmentId, CancellationToken ct);
    Task<(IReadOnlyList<Project> Items, int Total)> GetPagedAsync(
        string? keyword, int? departmentId, int page, int pageSize, CancellationToken ct);
    Task<bool> HasActiveByDepartmentAsync(int departmentId, CancellationToken ct);   // cho check giải tán xưởng
    Task<int> AddAsync(Project entity, CancellationToken ct);
    Task<bool> UpdateAsync(Project entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
