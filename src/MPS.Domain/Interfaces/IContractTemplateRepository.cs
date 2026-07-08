using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;

namespace MPS.Domain.Interfaces;

public interface IContractTemplateRepository
{
    Task<ContractTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ContractTemplate>> GetAllAsync(
        ContractKind? kind,
        ContractCategory? category,
        ContractSubtype? subtype,
        bool? isActive,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ContractTemplate> Items, int TotalCount)> GetPagedAsync(
        ContractKind? kind,
        ContractCategory? category,
        ContractSubtype? subtype,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<int> AddAsync(ContractTemplate entity, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(ContractTemplate entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hard delete nếu không có Contract tham chiếu; ngược lại set IsActive=false (soft).
    /// </summary>
    Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gắn file DOCX mới vào template: set IsCurrent=false cho file cũ (nếu có),
    /// insert <paramref name="file"/>, set template.TemplateFileId = newFile.Id, increment template.Version.
    /// </summary>
    Task<int> AttachTemplateFileAsync(int templateId, ContractFile file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load template kèm TemplateFile (MinioObjectKey để download).
    /// </summary>
    Task<ContractTemplate?> GetWithFileAsync(int id, CancellationToken cancellationToken = default);
}
