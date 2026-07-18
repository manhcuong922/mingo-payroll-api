using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface ICompanyPolicyRepository
{
    Task<(IReadOnlyList<CompanyPolicy> Items, int Total)> GetPagedAsync(
        bool? isActive,
        string? keyword,
        int page,
        int size,
        CancellationToken cancellationToken = default);

    Task<CompanyPolicy?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<CompanyPolicy?> GetByIdWithFilesAsync(int id, CancellationToken cancellationToken = default);

    Task<CompanyPolicyFile?> GetFileAsync(int policyId, int fileId, CancellationToken cancellationToken = default);

    Task<CompanyPolicyFile?> GetCurrentFileAsync(int policyId, CancellationToken cancellationToken = default);

    /// <summary>Insert policy and return its new Id (no file attached yet).</summary>
    Task<int> AddPolicyAsync(CompanyPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attach a new file as the current version. Demotes any previously-current
    /// file for this policy in the same transaction so the filtered unique index
    /// (1 file IsCurrent per policy) is never violated.
    /// </summary>
    Task<int> AddFileAsCurrentAsync(int policyId, CompanyPolicyFile file, CancellationToken cancellationToken = default);

    Task<bool> UpdateMetadataAsync(int id, string title, string? description, bool isActive,
        int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
