using MPS.Domain.Entities.Payroll;

namespace MPS.Domain.Interfaces.Payroll;

/// <summary>
/// Bậc hỗ trợ theo tiến độ của HĐ khoán việc (Phase 2). Cùng khuôn với
/// <see cref="IProductivityCoefficientRepository"/>.
/// </summary>
public interface IPieceWorkSupportBandRepository
{
    Task<IReadOnlyList<PieceWorkSupportBand>> GetAllAsync(bool? isActive, CancellationToken cancellationToken = default);
    Task<PieceWorkSupportBand?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> AddAsync(PieceWorkSupportBand entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(PieceWorkSupportBand entity, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
