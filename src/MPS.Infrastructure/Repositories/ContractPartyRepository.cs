using System.Data;
using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ContractPartyRepository : IContractPartyRepository
{
    private readonly AppDbContext _context;

    public ContractPartyRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ContractParty?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ContractParties
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContractParty>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken = default)
    {
        var query = _context.ContractParties.AsNoTracking();
        if (activeOnly)
            query = query.Where(p => p.EffectiveTo == null);

        return await query
            .OrderByDescending(p => p.EffectiveFrom)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CreateNewVersionAsync(ContractParty newVersion, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var now = DateTime.UtcNow;

        var activeRows = await _context.ContractParties
            .Where(p => p.EffectiveTo == null)
            .ToListAsync(cancellationToken);

        foreach (var row in activeRows)
        {
            row.EffectiveTo = now;
        }

        newVersion.EffectiveFrom = now;
        newVersion.EffectiveTo = null;
        newVersion.CreatedAt = now;

        await _context.ContractParties.AddAsync(newVersion, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return newVersion.Id;
    }
}
