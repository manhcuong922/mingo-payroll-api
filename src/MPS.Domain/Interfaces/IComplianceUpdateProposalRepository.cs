using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Domain.Interfaces;

/// <summary>Đọc proposal cập nhật pháp luật (chỉ đọc — module tự động chỉ ghi, admin duyệt ở module khác).</summary>
public interface IComplianceUpdateProposalRepository
{
    /// <summary>Danh sách proposal gần nhất, tuỳ chọn lọc theo <paramref name="status"/>.</summary>
    Task<IReadOnlyList<ComplianceUpdateProposal>> GetRecentAsync(string? status, int take, CancellationToken cancellationToken);
}
