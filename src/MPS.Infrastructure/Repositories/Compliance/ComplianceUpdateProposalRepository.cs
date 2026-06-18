using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class ComplianceUpdateProposalRepository : IComplianceUpdateProposalRepository
{
    private readonly AppDbContext _db;

    public ComplianceUpdateProposalRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ComplianceUpdateProposal>> GetRecentAsync(
        string? status, int take, CancellationToken cancellationToken)
    {
        var query = _db.ComplianceUpdateProposals.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(p => p.Status == status);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<ComplianceUpdateProposal?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _db.ComplianceUpdateProposals.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);
}
