using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

/// <summary>
/// Ví dụ Repository Interface - hợp đồng truy cập dữ liệu (Dependency Inversion).
/// </summary>
public interface IExampleRepository
{
    Task<ExampleEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> AddAsync(ExampleEntity entity, CancellationToken cancellationToken = default);
}
