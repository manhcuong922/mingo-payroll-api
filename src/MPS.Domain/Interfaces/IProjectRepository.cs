using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IProjectRepository
{
    // includeCancelled=false (mặc định, plan mục 6.5): ẩn đơn đã Cancelled (DeletedAt != null) khỏi danh sách.
    Task<IReadOnlyList<Project>> GetAllAsync(CancellationToken ct, bool includeCancelled = false);
    Task<Project?> GetByIdAsync(int id, CancellationToken ct);
    Task<Project?> GetByIdWithTasksAsync(int id, CancellationToken ct);   // include Tasks + DependsOn cho EV/PV
    Task<IReadOnlyList<Project>> GetByDepartmentAsync(int departmentId, CancellationToken ct, bool includeCancelled = false);
    Task<(IReadOnlyList<Project> Items, int Total)> GetPagedAsync(
        string? keyword, int? departmentId, int page, int pageSize, CancellationToken ct, bool includeCancelled = false);
    Task<bool> HasActiveByDepartmentAsync(int departmentId, CancellationToken ct);   // cho check giải tán xưởng
    Task<int> AddAsync(Project entity, CancellationToken ct);
    Task<bool> UpdateAsync(Project entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
