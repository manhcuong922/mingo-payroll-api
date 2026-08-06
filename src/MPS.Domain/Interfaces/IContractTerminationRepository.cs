using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;

namespace MPS.Domain.Interfaces;

/// <summary>
/// Phase 8 — Repository cho chấm dứt HĐ (Điều 34, 35, 36, 45 BLLĐ 2019).
/// 1-1 với Contract: 1 HĐ có tối đa 1 ContractTermination.
/// </summary>
public interface IContractTerminationRepository
{
    Task<ContractTermination?> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForContractAsync(int contractId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert <see cref="ContractTermination"/> + đặt parent <c>Contract.Status</c> theo
    /// <paramref name="targetStatus"/> (<see cref="ContractStatus.Terminated"/> nếu ngày chấm dứt
    /// đã qua, <see cref="ContractStatus.PendingTermination"/> nếu còn ở tương lai) trong cùng
    /// transaction. Reject nếu:
    ///   - Contract không tồn tại
    ///   - Contract.ArchivedAt != null
    ///   - Contract.Status != Active
    ///   - Đã có ContractTermination cho HĐ này (1-1)
    /// </summary>
    /// <returns>Id của ContractTermination vừa insert.</returns>
    Task<int> AddAndMarkContractTerminatedAsync(
        ContractTermination termination,
        ContractStatus targetStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// EmployeeInformationId (distinct) của các NV có bản ghi chấm dứt HĐ đã TỚI HẠN — tức
    /// <c>TerminationDate.Date &lt; <paramref name="todayVn"/>.Date</c> (đã qua ngày làm cuối
    /// theo giờ VN). Dùng cho job vô hiệu hoá tài khoản.
    /// </summary>
    Task<IReadOnlyList<int>> GetEmployeeInformationIdsWithMaturedTerminationAsync(
        DateTime todayVn, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promote HĐ đang <see cref="ContractStatus.PendingTermination"/> có TerminationDate đã qua
    /// (theo <paramref name="todayVn"/>) sang <see cref="ContractStatus.Terminated"/>. Trả số HĐ
    /// đã flip. Idempotent — chạy lại cùng ngày không đổi thêm.
    /// </summary>
    Task<int> PromoteMaturedPendingTerminationsAsync(
        DateTime todayVn, CancellationToken cancellationToken = default);
}
