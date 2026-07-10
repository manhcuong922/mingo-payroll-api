using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities.Evaluation;
using MPS.Domain.Enums.Evaluation;
using MPS.Domain.Interfaces.Evaluation;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories.Evaluation;

public class OrderEvaluationRepository : IOrderEvaluationRepository
{
    private readonly AppDbContext _context;

    public OrderEvaluationRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsForProjectAsync(int projectId, CancellationToken ct) =>
        _context.OrderEvaluations.AnyAsync(x => x.ProjectId == projectId, ct);

    public async Task<IReadOnlyList<int>> GetSubjectUserIdsForProjectAsync(
        int projectId, int subjectRoleId, CancellationToken ct) =>
        await _context.TaskAssignees
            .Where(a => a.Task!.ProjectId == projectId && a.User!.RoleId == subjectRoleId)
            .Select(a => a.UserId)
            .Distinct()
            .ToListAsync(ct);

    public Task<OrderEvaluation?> GetByIdWithGraphAsync(int id, CancellationToken ct) =>
        _context.OrderEvaluations
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Evaluator)
            .Include(x => x.Subjects).ThenInclude(s => s.Employee)
            .Include(x => x.Subjects).ThenInclude(s => s.Scores)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<OrderEvaluation?> GetByIdForUpdateAsync(int id, CancellationToken ct) =>
        _context.OrderEvaluations
            .Include(x => x.Subjects).ThenInclude(s => s.Scores)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<OrderEvaluation>> GetForEvaluatorAsync(
        int evaluatorUserId, EvaluationStatus? status, CancellationToken ct)
    {
        var query = _context.OrderEvaluations
            .AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.Subjects).ThenInclude(s => s.Employee)
            .Where(x => x.EvaluatorUserId == evaluatorUserId)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        return await query
            .OrderByDescending(x => x.PeriodYear)
            .ThenByDescending(x => x.PeriodMonth)
            .ThenByDescending(x => x.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<OrderEvaluationSubject>> GetSubmittedSubjectsByMonthAsync(
        int year, int month, int? onlyEmployeeUserId, CancellationToken ct)
    {
        var query = _context.OrderEvaluationSubjects
            .AsNoTracking()
            .Where(s => s.OrderEvaluation!.Status == EvaluationStatus.Submitted
                        && s.OrderEvaluation.PeriodYear == year
                        && s.OrderEvaluation.PeriodMonth == month
                        && s.FinalScore != null)
            .AsQueryable();

        if (onlyEmployeeUserId.HasValue)
            query = query.Where(s => s.EmployeeUserId == onlyEmployeeUserId.Value);

        return await query.ToListAsync(ct);
    }

    public async Task<int> AddAsync(OrderEvaluation entity, CancellationToken ct)
    {
        _context.OrderEvaluations.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<bool> UpdateWithConcurrencyAsync(
        OrderEvaluation entity, byte[] expectedRowVersion, CancellationToken ct)
    {
        _context.OrderEvaluations.Update(entity);
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
}
