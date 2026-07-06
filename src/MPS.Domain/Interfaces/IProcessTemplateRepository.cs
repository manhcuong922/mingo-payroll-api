using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IProcessTemplateRepository
{
    Task<IReadOnlyList<ProcessTemplate>> GetAllAsync(CancellationToken ct);
    Task<ProcessTemplate?> GetByIdWithStepsAsync(int id, CancellationToken ct);          // AsNoTracking — cho read
    Task<ProcessTemplate?> GetByIdWithStepsTrackedAsync(int id, CancellationToken ct);   // tracked — cho update
    Task<(IReadOnlyList<ProcessTemplate> Items, int Total)> GetPagedAsync(
        string? keyword, int page, int pageSize, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, int? excludeId, CancellationToken ct);
    Task<int> AddAsync(ProcessTemplate entity, CancellationToken ct);
    Task<bool> UpdateAsync(ProcessTemplate entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
}
