using MPS.Application.DTOs;

namespace MPS.Application.Interfaces;

/// <summary>
/// Ví dụ Service Interface - hợp đồng dịch vụ ở tầng Application.
/// </summary>
public interface IExampleService
{
    Task<ExampleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
