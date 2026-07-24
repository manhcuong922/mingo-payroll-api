using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

/// <summary>
/// Phase 7 — Repository cho phụ lục HĐ (Điều 22, 33 BLLĐ 2019).
/// Đặt tại Domain/Interfaces vì là contract truy cập dữ liệu, không phải cross-cutting service.
/// </summary>
public interface IContractAppendixRepository
{
    Task<ContractAppendix?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractAppendix>> GetByContractIdAsync(int contractId, CancellationToken cancellationToken = default);

    Task<bool> AppendixNoExistsForContractAsync(int contractId, string appendixNo, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert phụ lục mới ở trạng thái Draft. Caller phải tự set CreatedAt, CreatedByUserId, Status,
    /// NoticeSentAt trên entity trước khi gọi.
    /// </summary>
    Task<int> AddDraftAsync(ContractAppendix appendix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kích hoạt phụ lục (Draft → Active) trong cùng transaction với:
    ///  - Insert <see cref="ContractFile"/> (TargetType=Appendix, Version=1, IsCurrent=true)
    ///  - Apply <paramref name="apply"/> lên contract đã được tracked (delegate: caller mutate
    ///    contract + collection ContractAllowances ở trong scope của tx). Trả tracked
    ///    contract để callback đọc/sửa.
    /// Reject nếu:
    ///   - phụ lục không tồn tại / không Draft
    ///   - contract.ArchivedAt != null
    ///   - contract.Status != Active
    /// </summary>
    /// <returns>Id của <see cref="ContractFile"/> vừa insert.</returns>
    Task<int> ActivateDraftAsync(
        int appendixId,
        ContractFile file,
        Func<Contract, ICollection<ContractAllowance>, ContractAppendix, Task> apply,
        DateTime signedAtUtc,
        CancellationToken cancellationToken = default);
}
