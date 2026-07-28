using MPS.Domain.Entities;

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
    /// Insert <see cref="ContractTermination"/> + đặt parent <c>Contract.Status = Terminated</c>
    /// trong cùng transaction. Reject nếu:
    ///   - Contract không tồn tại
    ///   - Contract.ArchivedAt != null
    ///   - Contract.Status != Active
    ///   - Đã có ContractTermination cho HĐ này (1-1)
    /// </summary>
    /// <returns>Id của ContractTermination vừa insert.</returns>
    Task<int> AddAndMarkContractTerminatedAsync(
        ContractTermination termination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Case 7 — EmployeeInformationId (distinct) của các NV có bản ghi chấm dứt HĐ đã TỚI HẠN
    /// (<c>TerminationDate &lt;= asOfUtc</c>). Dùng cho job vô hiệu hoá tài khoản: chấm dứt có thể
    /// đặt ngày hiệu lực ở tương lai (thời gian báo trước) nên chỉ xử lý khi đã qua mốc đó.
    /// </summary>
    Task<IReadOnlyList<int>> GetEmployeeInformationIdsWithMaturedTerminationAsync(
        DateTime asOfUtc, CancellationToken cancellationToken = default);
}
