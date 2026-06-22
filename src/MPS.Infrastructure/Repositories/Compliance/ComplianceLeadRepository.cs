using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Compliance;

public sealed class ComplianceLeadRepository : IComplianceLeadRepository
{
    private readonly AppDbContext _db;

    public ComplianceLeadRepository(AppDbContext db) => _db = db;

    public async Task<int> AddAsync(ComplianceLead lead, CancellationToken cancellationToken)
    {
        _db.ComplianceLeads.Add(lead);
        await _db.SaveChangesAsync(cancellationToken);
        return lead.Id;
    }

    public async Task<IReadOnlyList<ComplianceLead>> GetRecentAsync(
        string? status, int take, CancellationToken cancellationToken)
    {
        var query = _db.ComplianceLeads.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(l => l.Status == status);

        return await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
