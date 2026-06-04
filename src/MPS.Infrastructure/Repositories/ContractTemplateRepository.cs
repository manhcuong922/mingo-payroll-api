using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums.Contract;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ContractTemplateRepository : IContractTemplateRepository
{
    private readonly AppDbContext _context;

    public ContractTemplateRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ContractTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ContractTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ContractTemplate>> GetAllAsync(
        ContractCategory? category,
        ContractSubtype? subtype,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ContractTemplates.AsNoTracking();
        if (category.HasValue)
            query = query.Where(t => t.ContractCategory == category.Value);
        if (subtype.HasValue)
            query = query.Where(t => t.ContractSubtype == subtype.Value);
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        return await query
            .OrderBy(t => t.Name)
            .ThenByDescending(t => t.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> AddAsync(ContractTemplate entity, CancellationToken cancellationToken = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        if (entity.Version <= 0) entity.Version = 1;
        await _context.ContractTemplates.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(ContractTemplate entity, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ContractTemplates
            .FirstOrDefaultAsync(t => t.Id == entity.Id, cancellationToken);
        if (existing is null) return false;

        existing.Name = entity.Name;
        existing.ContractCategory = entity.ContractCategory;
        existing.ContractSubtype = entity.ContractSubtype;
        existing.TemplateBody = entity.TemplateBody;
        existing.Version = existing.Version + 1;
        existing.IsActive = entity.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteOrDeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await _context.ContractTemplates
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (existing is null) return false;

        var referenced = await _context.Contracts.AnyAsync(c => c.TemplateId == id, cancellationToken);
        if (referenced)
        {
            existing.IsActive = false;
        }
        else
        {
            _context.ContractTemplates.Remove(existing);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
