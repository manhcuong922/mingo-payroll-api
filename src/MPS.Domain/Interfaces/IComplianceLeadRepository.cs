using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Domain.Interfaces;

/// <summary>Đọc/ghi Lead do watcher chủ động (Kênh B) sinh ra.</summary>
public interface IComplianceLeadRepository
{
    Task<int> AddAsync(ComplianceLead lead, CancellationToken cancellationToken);

    Task<IReadOnlyList<ComplianceLead>> GetRecentAsync(string? status, int take, CancellationToken cancellationToken);
}
