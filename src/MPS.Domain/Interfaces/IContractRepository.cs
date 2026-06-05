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
}
