using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

public interface IProductivityCoefficientRepository
{
    Task<IReadOnlyList<ProductivityCoefficient>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<ProductivityCoefficient?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> AddAsync(ProductivityCoefficient entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ProductivityCoefficient entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
