using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;

namespace MPS.Domain.Interfaces;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(int id, bool includeAllowances = false, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Contract> Items, int Total)> GetPagedAsync(
        int? employeeId,
        ContractStatus? status,
        ContractCategory? category,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<bool> ContractNoExistsAsync(string contractNo, int? excludeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Số HĐ XĐ thời hạn (Definite+Official) đã/đang tồn tại của 1 NLĐ với 1 NSDLĐ
    /// (trạng thái Active/Expired/Terminated). Dùng cho rule Điều 20 k.2c.
    /// </summary>
    Task<int> CountDefiniteOfficialContractsAsync(
        int employeeId,
        int employerPartyId,
        int? excludeContractId,
        CancellationToken cancellationToken = default);

    Task<int> AddAsync(Contract contract, IEnumerable<ContractAllowance> allowances, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update HĐ + tái lập danh sách ContractAllowances (replace all).
    /// Chỉ cho phép khi Status=Draft.
    /// </summary>
    /// <returns>true nếu update; false nếu không tìm thấy; throw DomainValidationException nếu Status != Draft.</returns>
    Task<bool> UpdateDraftAsync(Contract contract, IEnumerable<ContractAllowance> allowances, CancellationToken cancellationToken = default);

    /// <summary>Hard-delete HĐ DRAFT (kèm allowances). Throws nếu Status != Draft.</summary>
    Task<bool> DeleteDraftAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Set Status=Cancelled cho HĐ DRAFT. Throws nếu Status != Draft.</summary>
    Task<bool> CancelDraftAsync(int id, CancellationToken cancellationToken = default);

    // Allowance sub-operations
    Task<ContractAllowance?> GetAllowanceAsync(int contractId, int allowanceTypeId, CancellationToken cancellationToken = default);
    Task<bool> AddAllowanceAsync(int contractId, ContractAllowance allowance, CancellationToken cancellationToken = default);
    Task<bool> UpdateAllowanceAmountAsync(int contractId, int allowanceTypeId, decimal amount, string currency, string? note, CancellationToken cancellationToken = default);
    Task<bool> RemoveAllowanceAsync(int contractId, int allowanceTypeId, CancellationToken cancellationToken = default);

    // ===== Phase 6 — Activation + File replace =====

    /// <summary>
    /// Atomically transition a DRAFT contract → ACTIVE. Persists the supplied
    /// <see cref="ContractFile"/> row (Version=1, IsCurrent=true) and freezes
    /// snapshot lương + signedAt/signedPlace onto the contract.
    /// </summary>
    /// <returns>The new ContractFile.Id.</returns>
    /// <exception cref="DomainValidationException">If contract not Draft, archived, or not found.</exception>
    Task<int> ActivateDraftAsync(
        int contractId,
        decimal snapshotAmount,
        string snapshotCurrency,
        DateTime signedAtUtc,
        string signedPlace,
        ContractFile file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current (IsCurrent=true) ContractFile for the given contract+target.
    /// Returns null if none exists.
    /// </summary>
    Task<ContractFile?> GetCurrentFileAsync(
        int contractId,
        Domain.Enums.Contract.FileTargetType targetType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically replace the current file: set existing IsCurrent=false, insert
    /// <paramref name="newFile"/> with Version=oldVersion+1 and IsCurrent=true.
    /// Rejects if contract is archived.
    /// </summary>
    /// <returns>The new ContractFile.Id.</returns>
    Task<int> ReplaceCurrentFileAsync(
        int contractId,
        Domain.Enums.Contract.FileTargetType targetType,
        ContractFile newFile,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 8 — Upload văn bản (vd: thông báo chấm dứt HĐLĐ — Điều 45 BLLĐ 2019)
    /// gắn với HĐ Active mà chưa có record nghiệp vụ tham chiếu (TargetId = contractId).
    /// Đặt prior IsCurrent=true (cùng TargetType+TargetId) thành false rồi insert
    /// <paramref name="file"/> với Version=oldMax+1, IsCurrent=true.
    /// Reject nếu HĐ archived hoặc Status != Active.
    /// </summary>
    Task<int> AddContractFileForActiveContractAsync(
        int contractId,
        ContractFile file,
        CancellationToken cancellationToken = default);
}
