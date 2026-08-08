using Microsoft.EntityFrameworkCore;
using MPS.Domain.Common;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Interfaces.Evaluation;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Evaluation;

public class MonthlyEvaluationRepository : IMonthlyEvaluationRepository
{
    private readonly AppDbContext _context;

    public MonthlyEvaluationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<MonthlyEvaluation?> GetByIdWithGraphAsync(int id, CancellationToken ct) =>
        _context.MonthlyEvaluations
            .AsNoTracking()
            .Include(m => m.Employee)
            .Include(m => m.SourceLinks)
            .Include(m => m.Scores)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<MonthlyEvaluation?> GetByIdForUpdateAsync(int id, CancellationToken ct) =>
        _context.MonthlyEvaluations
            .Include(m => m.SourceLinks)
            .Include(m => m.Scores)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<MonthlyEvaluation?> GetByEmployeeMonthAsync(
        int employeeUserId, int year, int month, CancellationToken ct) =>
        _context.MonthlyEvaluations
            .AsNoTracking()
            .Include(m => m.SourceLinks)
            .Include(m => m.Scores)
            .FirstOrDefaultAsync(
                m => m.EmployeeUserId == employeeUserId && m.PeriodYear == year && m.PeriodMonth == month, ct);

    public async Task<(IReadOnlyList<MonthlyEvaluation> Items, int Total)> GetPagedByMonthAsync(
        int year, int month, int? departmentId, int page, int pageSize, CancellationToken ct)
    {
        var query = _context.MonthlyEvaluations
            .AsNoTracking()
            .Include(m => m.Employee)
            .Where(m => m.PeriodYear == year && m.PeriodMonth == month)
            .AsQueryable();

        if (departmentId.HasValue)
            query = query.Where(m => _context.EmployeeInformations
                .Any(ei => ei.UserId == m.EmployeeUserId && ei.DepartmentId == departmentId.Value));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(m => m.EmployeeUserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<MonthlyEvaluation>> GetForEmployeeAsync(int employeeUserId, CancellationToken ct) =>
        await _context.MonthlyEvaluations
            .AsNoTracking()
            .Include(m => m.Scores)
            .Include(m => m.SourceLinks)
            .Where(m => m.EmployeeUserId == employeeUserId)
            .OrderByDescending(m => m.PeriodYear)
            .ThenByDescending(m => m.PeriodMonth)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<int>> GetExistingEmployeeUserIdsForMonthAsync(
        int year, int month, CancellationToken ct) =>
        await _context.MonthlyEvaluations
            .AsNoTracking()
            .Where(m => m.PeriodYear == year && m.PeriodMonth == month)
            .Select(m => m.EmployeeUserId)
            .Distinct()
            .ToListAsync(ct);

    public async Task<int> AddAsync(MonthlyEvaluation entity, CancellationToken ct)
    {
        _context.MonthlyEvaluations.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<int> AddRangeAsync(IReadOnlyList<MonthlyEvaluation> entities, CancellationToken ct)
    {
        if (entities.Count == 0) return 0;

        _context.MonthlyEvaluations.AddRange(entities);
        await _context.SaveChangesAsync(ct);
        return entities.Count;
    }

    public async Task<bool> UpdateWithConcurrencyAsync(
        MonthlyEvaluation entity, byte[] expectedRowVersion, CancellationToken ct)
    {
        _context.MonthlyEvaluations.Update(entity);
        _context.Entry(entity).Property(e => e.RowVersion).OriginalValue = expectedRowVersion;

        try
        {
            return await _context.SaveChangesAsync(ct) > 0;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task<MonthlyAggregateOutcome> UpsertAggregatedAsync(
        int employeeUserId, int year, int month, int subjectRoleId, int actorUserId,
        decimal finalScore, IReadOnlyList<AggregatedContribution> contributions, CancellationToken ct)
    {
        var existing = await _context.MonthlyEvaluations
            .Include(m => m.SourceLinks)
            .FirstOrDefaultAsync(
                m => m.EmployeeUserId == employeeUserId && m.PeriodYear == year && m.PeriodMonth == month, ct);

        if (existing is null)
        {
            var entity = new MonthlyEvaluation
            {
                EmployeeUserId = employeeUserId,
                PeriodYear = year,
                PeriodMonth = month,
                SubjectRoleIdSnapshot = subjectRoleId,
                Source = EvaluationSource.Aggregated,
                FinalScore = finalScore,
                Status = EvaluationStatus.Draft,
                CreatedByUserId = actorUserId,
                CreatedAt = VietnamClock.Now,
                SourceLinks = contributions
                    .Select(c => new MonthlyEvaluationSourceLink
                    {
                        OrderEvaluationSubjectId = c.OrderEvaluationSubjectId,
                        ContributedScore = c.Score,
                    })
                    .ToList(),
            };
            _context.MonthlyEvaluations.Add(entity);
            await _context.SaveChangesAsync(ct);
            return MonthlyAggregateOutcome.Created;
        }

        if (existing.Status == EvaluationStatus.Approved)
            return MonthlyAggregateOutcome.SkippedApproved;   // đã khóa — cần HR Reopen (E12)

        existing.Source = EvaluationSource.Aggregated;
        existing.FinalScore = finalScore;
        existing.Status = EvaluationStatus.Draft;
        existing.SubjectRoleIdSnapshot = subjectRoleId;

        var oldLinks = existing.SourceLinks.ToList();
        _context.MonthlyEvaluationSourceLinks.RemoveRange(oldLinks);
        existing.SourceLinks.Clear();
        foreach (var c in contributions)
            existing.SourceLinks.Add(new MonthlyEvaluationSourceLink
            {
                OrderEvaluationSubjectId = c.OrderEvaluationSubjectId,
                ContributedScore = c.Score,
            });

        await _context.SaveChangesAsync(ct);
        return MonthlyAggregateOutcome.Updated;
    }
}
