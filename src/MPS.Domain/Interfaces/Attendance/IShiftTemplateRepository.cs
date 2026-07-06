using MPS.Domain.Entities.Attendance;

namespace MPS.Domain.Interfaces.Attendance;

public interface IShiftTemplateRepository
{
    Task<(IReadOnlyList<ShiftTemplate> Items, int Total)> GetPagedAsync(
        bool? isActive,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<ShiftTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup cho import bảng phân ca: trả về map Code → Id, CHỈ shift active.
    /// Match exact (collation DB quyết case-sensitive); caller normalize input nếu cần.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetActiveIdsByCodesAsync(
        IEnumerable<string> codes,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByCodeAsync(string code, int? excludeId, CancellationToken cancellationToken = default);

    Task<bool> ExistsByNameAsync(string name, int? excludeId, CancellationToken cancellationToken = default);

    Task<int> AddAsync(ShiftTemplate entity, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(ShiftTemplate entity, CancellationToken cancellationToken = default);

    Task<bool> DeactivateAsync(int id, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);
}
