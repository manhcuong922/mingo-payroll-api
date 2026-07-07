using System.Data;
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
        ContractKind? kind,
        ContractCategory? category,
        ContractSubtype? subtype,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ContractTemplate> query = _context.ContractTemplates.AsNoTracking();
        if (kind.HasValue)
            query = query.Where(t => t.Kind == kind.Value);
        if (category.HasValue)
            query = query.Where(t => t.ContractCategory == category.Value);
        if (subtype.HasValue)
            query = query.Where(t => t.ContractSubtype == subtype.Value);
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        return await query
            .Include(t => t.TemplateFile)
            .OrderBy(t => t.Name)
            .ThenByDescending(t => t.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ContractTemplate> Items, int TotalCount)> GetPagedAsync(
        ContractKind? kind,
        ContractCategory? category,
        ContractSubtype? subtype,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ContractTemplate> query = _context.ContractTemplates.AsNoTracking();
        if (kind.HasValue)
            query = query.Where(t => t.Kind == kind.Value);
        if (category.HasValue)
            query = query.Where(t => t.ContractCategory == category.Value);
        if (subtype.HasValue)
            query = query.Where(t => t.ContractSubtype == subtype.Value);
        if (isActive.HasValue)
            query = query.Where(t => t.IsActive == isActive.Value);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(t => t.TemplateFile)
            .OrderBy(t => t.Name)
            .ThenByDescending(t => t.Version)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
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
        existing.Kind = entity.Kind;
        existing.ContractCategory = entity.ContractCategory;
        existing.ContractSubtype = entity.ContractSubtype;
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

    public Task<ContractTemplate?> GetWithFileAsync(int id, CancellationToken cancellationToken = default) =>
        _context.ContractTemplates
            .AsNoTracking()
            .Include(t => t.TemplateFile)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<int> AttachTemplateFileAsync(int templateId, ContractFile file, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var template = await _context.ContractTemplates
            .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken);

        if (template is null)
        {
            await tx.RollbackAsync(cancellationToken);
            throw new Domain.Exceptions.DomainValidationException($"TEMPLATE_NOT_FOUND: {templateId}");
        }

        // Mark any existing template file as not current.
        await _context.ContractFiles
            .Where(f => f.TargetType == FileTargetType.Template && f.TargetId == templateId && f.IsCurrent)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.IsCurrent, false), cancellationToken);

        await _context.ContractFiles.AddAsync(file, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        template.TemplateFileId = file.Id;
        template.Version = file.Version;
        await _context.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return file.Id;
    }
}
