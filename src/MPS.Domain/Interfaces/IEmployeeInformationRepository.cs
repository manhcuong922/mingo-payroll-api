using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IEmployeeInformationRepository
{
    Task<EmployeeInformation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeInformation?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeInformation>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
