using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;

namespace MPS.Domain.Interfaces;

public interface IContractRepository
{
    Task<Contract?> GetByIdAsync(int id, bool includeAllowances = false, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Contract> Items, int Total)> GetPagedAsync(
        int? employeeInformationId,
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
        int employeeInformationId,
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

    /// <summary>
    /// Lấy tất cả ContractFile match (TargetType, TargetId), sort Version DESC.
    /// Trả empty list nếu không có file (không throw).
    /// </summary>
    Task<IReadOnlyList<ContractFile>> GetFilesByTargetAsync(
        Domain.Enums.Contract.FileTargetType targetType,
        int targetId,
        CancellationToken cancellationToken = default);

    // ===== Phase 9 — Background jobs =====

    /// <summary>
    /// Quick read for the archive guard pipeline behavior: returns <c>ArchivedAt</c>
    /// (null if not archived). Returns null tuple if contract not found — caller decides
    /// how to handle absence (typically downstream handler will throw 4xx itself).
    /// </summary>
    Task<DateTime?> GetArchivedAtAsync(int contractId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra NLĐ có ít nhất 1 HĐ Status=Active không. Dùng khi promote employee → create User.
    /// </summary>
    Task<bool> HasActiveContractAsync(int employeeInformationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load HĐ kèm toàn bộ dữ liệu cần thiết để sinh DOCX: EI, EmployerParty, Position,
    /// SalaryGrade, ContractAllowances + AllowanceType, Template + TemplateFile.
    /// </summary>
    Task<Contract?> GetForDocumentGenerationAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Điều 20 k.2.b BLLĐ 2019 — HĐ Definite + Status=Active mà
    /// <c>EffectiveTo + 30 ngày &lt; nowUtc</c> thì phải auto-convert sang Indefinite
    /// (set <c>ConvertedFromDefiniteFlag=1</c>, <c>Category=Indefinite</c>,
    /// <c>EffectiveTo=null</c>, <c>UpdatedAt=nowUtc</c>).
    /// </summary>
    /// <returns>Số HĐ đã chuyển đổi.</returns>
    Task<int> AutoConvertExpiredDefiniteAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// HĐ Status=Active có <c>EffectiveTo &lt; nowUtc</c> mà KHÔNG bị
    /// <see cref="AutoConvertExpiredDefiniteAsync"/> bắt (đã có HĐ con — quan hệ
    /// <c>ParentContractId</c>) → set Status=Expired, UpdatedAt=nowUtc.
    /// </summary>
    /// <returns>Số HĐ đã expire.</returns>
    Task<int> ExpireOverdueContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set <c>ArchivedAt = nowUtc</c> cho:
    ///  - HĐ Terminated mà <c>ContractTermination.TerminationDate ≤ nowUtc - 10y</c>
    ///  - HĐ Expired mà <c>EffectiveTo ≤ nowUtc - 10y</c>
    /// (Điều 7 NĐ 152/2020 + Luật Lưu trữ — lưu HĐLĐ 10 năm sau khi chấm dứt).
    /// </summary>
    /// <returns>Số HĐ đã archive.</returns>
    Task<int> ArchiveOldContractsAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
}
