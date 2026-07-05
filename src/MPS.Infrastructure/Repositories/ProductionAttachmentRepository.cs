using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ProductionAttachmentRepository : IProductionAttachmentRepository
{
    private readonly AppDbContext _context;

    public ProductionAttachmentRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProductionAttachment>> GetByOwnerAsync(
        AttachmentOwnerType ownerType, int ownerId, CancellationToken ct) =>
        await _context.ProductionAttachments
            .AsNoTracking()
            .Include(a => a.UploadedBy)
            .Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId)
            .OrderByDescending(a => a.UploadedAt)
            .ToListAsync(ct);

    public Task<ProductionAttachment?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.ProductionAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<int> CountByOwnerAsync(AttachmentOwnerType ownerType, int ownerId, CancellationToken ct) =>
        _context.ProductionAttachments.CountAsync(a => a.OwnerType == ownerType && a.OwnerId == ownerId, ct);

    public async Task<int> AddAsync(ProductionAttachment entity, CancellationToken ct)
    {
        _context.ProductionAttachments.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _context.ProductionAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (entity is null) return false;
        _context.ProductionAttachments.Remove(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }
}
