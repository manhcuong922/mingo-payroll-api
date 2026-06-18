using MPS.Domain.Entities.Compliance.AutoUpdate;

namespace MPS.Domain.Interfaces;

/// <summary>Đọc/cập nhật proposal cập nhật pháp luật (module tự động tạo proposal; admin duyệt).</summary>
public interface IComplianceUpdateProposalRepository
{
    /// <summary>Danh sách proposal gần nhất, tuỳ chọn lọc theo <paramref name="status"/>.</summary>
    Task<IReadOnlyList<ComplianceUpdateProposal>> GetRecentAsync(string? status, int take, CancellationToken cancellationToken);

    /// <summary>Lấy 1 proposal (tracked) để cập nhật trạng thái duyệt.</summary>
    Task<ComplianceUpdateProposal?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>Lưu thay đổi đang track (vd sau khi đổi Status/ReviewNote).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
