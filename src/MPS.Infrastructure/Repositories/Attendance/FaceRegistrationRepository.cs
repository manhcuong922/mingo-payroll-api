using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Enums.Attendance;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class FaceRegistrationRepository : IFaceRegistrationRepository
{
    private readonly AppDbContext _context;

    public FaceRegistrationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<FaceRegistration?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _context.FaceRegistrations
            .AsNoTracking()
            .Include(f => f.EmployeeInformation)
            .Include(f => f.ReviewedByUser)
            .FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<FaceRegistration>> GetByEmployeeIdAsync(int employeeId, CancellationToken ct = default) =>
        await _context.FaceRegistrations
            .AsNoTracking()
            .Include(f => f.EmployeeInformation)
            .Include(f => f.ReviewedByUser)
            .Where(f => f.EmployeeInformationId == employeeId)
            .OrderByDescending(f => f.SubmittedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FaceRegistration>> GetAllAsync(FaceRegistrationStatus? status, CancellationToken ct = default)
    {
        var query = _context.FaceRegistrations
            .AsNoTracking()
            .Include(f => f.EmployeeInformation)
            .Include(f => f.ReviewedByUser)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(f => f.Status == status.Value);

        return await query.OrderByDescending(f => f.SubmittedAt).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FaceRegistration>> GetApprovedWithEmbeddingsAsync(CancellationToken ct = default) =>
        await _context.FaceRegistrations
            .AsNoTracking()
            .Include(f => f.EmployeeInformation)
                .ThenInclude(ei => ei.User)
            .Where(f => f.Status == FaceRegistrationStatus.Approved)
            .OrderBy(f => f.EmployeeInformationId)
            .ToListAsync(ct);

    public async Task AddAsync(FaceRegistration entity, CancellationToken ct = default)
    {
        await _context.FaceRegistrations.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(FaceRegistration entity, CancellationToken ct = default)
    {
        var tracked = await _context.FaceRegistrations.FirstOrDefaultAsync(f => f.Id == entity.Id, ct);
        if (tracked is null) return false;

        tracked.Status = entity.Status;
        tracked.RejectReason = entity.RejectReason;
        tracked.ReviewedAt = entity.ReviewedAt;
        tracked.ReviewedByUserId = entity.ReviewedByUserId;

        await _context.SaveChangesAsync(ct);
        return true;
    }
}
