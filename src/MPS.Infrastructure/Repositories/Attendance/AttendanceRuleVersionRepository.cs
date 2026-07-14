using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Attendance;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Attendance;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Attendance;

public class AttendanceRuleVersionRepository : IAttendanceRuleVersionRepository
{
    private readonly AppDbContext _context;

    public AttendanceRuleVersionRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<AttendanceRuleVersion?> GetAtAsync(int ruleId, DateOnly workDate, CancellationToken cancellationToken = default) =>
        _context.AttendanceRuleVersions
            .AsNoTracking()
            .Where(v => v.AttendanceRuleId == ruleId
                     && v.EffectiveFrom <= workDate
                     && (v.EffectiveTo == null || v.EffectiveTo >= workDate))
            .OrderByDescending(v => v.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<AttendanceRuleVersion?> GetOpenAsync(int ruleId, CancellationToken cancellationToken = default) =>
        _context.AttendanceRuleVersions
            .AsNoTracking()
            .Where(v => v.AttendanceRuleId == ruleId && v.EffectiveTo == null)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<int, AttendanceRuleVersion>> GetOpenByRuleIdsAsync(
        IReadOnlyCollection<int> ruleIds, CancellationToken cancellationToken = default)
    {
        if (ruleIds.Count == 0)
            return new Dictionary<int, AttendanceRuleVersion>();

        var rows = await _context.AttendanceRuleVersions
            .AsNoTracking()
            .Where(v => ruleIds.Contains(v.AttendanceRuleId) && v.EffectiveTo == null)
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(v => v.AttendanceRuleId);
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyList<AttendanceRuleVersion>>> GetByRuleIdsAsync(
        IReadOnlyCollection<int> ruleIds, CancellationToken cancellationToken = default)
    {
        if (ruleIds.Count == 0)
            return new Dictionary<int, IReadOnlyList<AttendanceRuleVersion>>();

        var rows = await _context.AttendanceRuleVersions
            .AsNoTracking()
            .Where(v => ruleIds.Contains(v.AttendanceRuleId))
            .OrderBy(v => v.AttendanceRuleId)
            .ThenBy(v => v.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(v => v.AttendanceRuleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AttendanceRuleVersion>)g.ToList());
    }

    public async Task<int> AddAsync(AttendanceRuleVersion entity, CancellationToken cancellationToken = default)
    {
        // Chặn overlap: version mới phải nằm ngoài mọi khoảng [EffectiveFrom, EffectiveTo|∞] hiện có.
        var newStart = entity.EffectiveFrom;
        var newEnd = entity.EffectiveTo; // null → open

        var conflicts = await _context.AttendanceRuleVersions
            .AsNoTracking()
            .Where(v => v.AttendanceRuleId == entity.AttendanceRuleId)
            .Where(v =>
                // v.From <= newEnd (nếu newEnd null → luôn true) AND v.To >= newStart (nếu v.To null → luôn true)
                (newEnd == null || v.EffectiveFrom <= newEnd) &&
                (v.EffectiveTo == null || v.EffectiveTo >= newStart))
            .AnyAsync(cancellationToken);

        if (conflicts)
            throw new DomainValidationException(
                $"AttendanceRuleVersion overlap: rule {entity.AttendanceRuleId} đã có version phủ khoảng {newStart:yyyy-MM-dd}..{(newEnd?.ToString("yyyy-MM-dd") ?? "∞")}.");

        await _context.AttendanceRuleVersions.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task CloseOpenVersionAsync(int ruleId, DateOnly effectiveTo, int updatedByUserId, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var tracked = await _context.AttendanceRuleVersions
            .FirstOrDefaultAsync(v => v.AttendanceRuleId == ruleId && v.EffectiveTo == null, cancellationToken);
        if (tracked is null) return;

        if (effectiveTo < tracked.EffectiveFrom)
            throw new DomainValidationException(
                $"EffectiveTo ({effectiveTo:yyyy-MM-dd}) không được nhỏ hơn EffectiveFrom ({tracked.EffectiveFrom:yyyy-MM-dd}).");

        tracked.EffectiveTo = effectiveTo;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
