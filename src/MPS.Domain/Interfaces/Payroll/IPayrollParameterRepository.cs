using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

public interface IPayrollParameterRepository
{
    Task<IReadOnlyList<PayrollParameter>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<PayrollParameter?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PayrollParameter?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<int> AddAsync(PayrollParameter entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PayrollParameter entity, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(string key, int? excludeId, CancellationToken cancellationToken = default);
}
