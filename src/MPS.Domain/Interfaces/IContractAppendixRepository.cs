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
    /// Đếm số phụ lục hiện có của 1 HĐ (mọi trạng thái, kể cả Draft). Dùng để sinh số phụ lục
    /// kế tiếp theo dạng <c>{ContractNo}-PL-{seq}</c>. Không loại bỏ Draft — HR tạo Draft rồi
    /// huỷ giữa chừng thì số vẫn nhảy tiếp để tránh trùng lịch sử.
    /// </summary>
    Task<int> CountForContractAsync(int contractId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Ký + upload PDF nhưng KHÔNG apply changes lên HĐ (Draft → Scheduled). Dùng khi phụ lục có
    /// <c>EffectiveFrom</c> ở tương lai — job <c>AppendixEffectiveDateJob</c> sẽ apply và promote
    /// sang Active khi đến ngày. Kiểm tra parent contract giống <see cref="ActivateDraftAsync"/>.
    /// </summary>
    Task<int> ScheduleDraftAsync(
        int appendixId,
        ContractFile file,
        DateTime signedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy tất cả phụ lục đang Scheduled có <c>EffectiveFrom &lt;= nowUtc</c> — job dùng để chọn
    /// batch cần promote. Chỉ trả các phụ lục thuộc HĐ Active và không bị archived.
    /// </summary>
    Task<IReadOnlyList<int>> GetDueScheduledAppendixIdsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Promote 1 phụ lục Scheduled → Active: apply changes lên HĐ trong cùng transaction.
    /// Reject nếu appendix không tồn tại / không phải Scheduled / parent contract không còn Active.
    /// </summary>
    Task PromoteScheduledAsync(
        int appendixId,
        Func<Contract, ICollection<ContractAllowance>, ContractAppendix, Task> apply,
        CancellationToken cancellationToken = default);
}
