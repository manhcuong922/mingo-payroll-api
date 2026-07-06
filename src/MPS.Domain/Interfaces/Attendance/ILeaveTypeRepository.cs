using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface ILeaveTypeRepository
{
    Task<(IReadOnlyList<LeaveType> Items, int Total)> GetPagedAsync(
        bool? isActive,
        string? search,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<LeaveType?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<LeaveType?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveType>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(LeaveType entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(LeaveType entity, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
