using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IEmployeeInformationRepository
{
    Task<EmployeeInformation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EmployeeInformation?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeInformation>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<EmployeeInformation> Items, int TotalCount)> GetPagedAsync(
        string? keyword,
        int? positionId,
        bool? hasAccount,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> PositionExistsAsync(int positionId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, int? excludeId = null, CancellationToken cancellationToken = default);
    Task<string?> GetPositionCodeAsync(int positionId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(EmployeeInformation entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
