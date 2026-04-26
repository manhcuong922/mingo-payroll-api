using MPS.Application.DTOs;
using MPS.Application.Interfaces;

namespace MPS.Infrastructure.Services;

/// <summary>
/// Ví dụ Service Implementation - hiện thực hóa logic dịch vụ bên ngoài.
/// </summary>
public class ExampleService : IExampleService
{
    public Task<ExampleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual service logic
        return Task.FromResult<ExampleDto?>(null);
    }
}
