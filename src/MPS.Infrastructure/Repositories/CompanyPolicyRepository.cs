using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class CompanyPolicyRepository : ICompanyPolicyRepository
{
    private readonly AppDbContext _context;

    public CompanyPolicyRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<CompanyPolicy> Items, int Total)> GetPagedAsync(
        bool? isActive,
        string? keyword,
        int page,
        int size,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CompanyPolicies
            .AsNoTracking()
            .Include(p => p.Files)
            .AsQueryable();

        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(p => EF.Functions.Like(p.Title, $"%{k}%"));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<CompanyPolicy?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.CompanyPolicies.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<CompanyPolicy?> GetByIdWithFilesAsync(int id, CancellationToken cancellationToken = default) =>
        _context.CompanyPolicies
            .AsNoTracking()
            .Include(p => p.Files)
            .ThenInclude(f => f.UploadedByUser)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<CompanyPolicyFile?> GetFileAsync(int policyId, int fileId, CancellationToken cancellationToken = default) =>
        _context.CompanyPolicyFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fileId && f.CompanyPolicyId == policyId, cancellationToken);

    public Task<CompanyPolicyFile?> GetCurrentFileAsync(int policyId, CancellationToken cancellationToken = default) =>
        _context.CompanyPolicyFiles
            .AsNoTracking()
            .Where(f => f.CompanyPolicyId == policyId && f.IsCurrent)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> AddPolicyAsync(CompanyPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.CompanyPolicies.AddAsync(policy, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return policy.Id;
    }

    public async Task<int> AddFileAsCurrentAsync(int policyId, CompanyPolicyFile file, CancellationToken cancellationToken = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        var existing = await _context.CompanyPolicyFiles
            .Where(f => f.CompanyPolicyId == policyId)
            .ToListAsync(cancellationToken);

        var nextVersion = existing.Count == 0 ? 1 : existing.Max(f => f.Version) + 1;
        foreach (var prev in existing)
            prev.IsCurrent = false;

        // Demote previous current row first (SaveChanges), then insert new row
        // with IsCurrent=true to keep the filtered-unique index satisfied at all
        // times within the transaction.
        await _context.SaveChangesAsync(cancellationToken);

        file.CompanyPolicyId = policyId;
        file.Version = nextVersion;
        file.IsCurrent = true;
        await _context.CompanyPolicyFiles.AddAsync(file, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        await tx.CommitAsync(cancellationToken);
        return file.Id;
    }

    public async Task<bool> UpdateMetadataAsync(
        int id, string title, string? description, bool isActive,
        int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.CompanyPolicies.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (tracked is null) return false;

        tracked.Title = title;
        tracked.Description = description;
        tracked.IsActive = isActive;
        tracked.UpdatedAt = nowUtc;
        tracked.UpdatedByUserId = updatedByUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.CompanyPolicies
            .Include(p => p.Files)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (tracked is null) return false;

        _context.CompanyPolicyFiles.RemoveRange(tracked.Files);
        _context.CompanyPolicies.Remove(tracked);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
