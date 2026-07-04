using Microsoft.EntityFrameworkCore;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Repositories;

public class ProjectTaskRepository : IProjectTaskRepository
{
    private readonly AppDbContext _context;

    public ProjectTaskRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<ProjectTask?> GetByIdAsync(int id, CancellationToken ct) =>
        _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct) =>
        _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Include(t => t.CreatedBy)
            .Include(t => t.CompletedBy)
            .Include(t => t.Department)
            .Include(t => t.Children).ThenInclude(c => c.Assignees).ThenInclude(a => a.User)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct) =>
        await _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.StepOrder).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(int departmentId, CancellationToken ct) =>
        await _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Where(t => t.DepartmentId == departmentId)
            .OrderBy(t => t.StepOrder).ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct) =>
        await _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Department)
            .Include(t => t.Assignees)
            .Where(t => !t.IsBatchParent && t.Assignees.Any(a => a.UserId == userId))
            .OrderBy(t => t.Deadline)
            .ToListAsync(ct);

    public Task<bool> HasChildrenAsync(int taskId, CancellationToken ct) =>
        _context.ProjectTasks.AnyAsync(t => t.ParentTaskId == taskId, ct);

    public async Task<bool> AllChildrenDoneAsync(int parentTaskId, CancellationToken ct)
    {
        var hasChildren = await _context.ProjectTasks.AnyAsync(t => t.ParentTaskId == parentTaskId, ct);
        if (!hasChildren) return false;
        return !await _context.ProjectTasks
            .AnyAsync(t => t.ParentTaskId == parentTaskId && t.Status != TaskItemStatus.Done, ct);
    }

    public Task<int> CountActiveByUserAsync(int userId, CancellationToken ct) =>
        _context.ProjectTasks.CountAsync(
            t => !t.IsBatchParent
                && t.Status != TaskItemStatus.Done
                && t.Assignees.Any(a => a.UserId == userId), ct);

    public Task<int> SumChildrenPlannedQuantityAsync(int parentTaskId, int? excludeTaskId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(c => c.ParentTaskId == parentTaskId && c.Id != (excludeTaskId ?? 0))
            .SumAsync(c => c.PlannedQuantity, ct);

    public async Task<int> AddAsync(ProjectTask entity, CancellationToken ct)
    {
        _context.ProjectTasks.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task AddRangeAsync(IEnumerable<ProjectTask> entities, CancellationToken ct)
    {
        _context.ProjectTasks.AddRange(entities);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> UpdateAsync(ProjectTask entity, CancellationToken ct)
    {
        _context.ProjectTasks.Update(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }

    public async Task<bool> UpdateWithConcurrencyAsync(ProjectTask entity, byte[] expectedRowVersion, CancellationToken ct)
    {
        _context.ProjectTasks.Update(entity);
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

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _context.ProjectTasks.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (entity is null) return false;
        _context.ProjectTasks.Remove(entity);
        return await _context.SaveChangesAsync(ct) > 0;
    }

    // ⭐ Cha tự cộng: CompletedQuantity của cha = SUM(CompletedQuantity các con).
    public async Task RecomputeParentCompletedAsync(int parentTaskId, CancellationToken ct)
    {
        var total = await _context.ProjectTasks
            .Where(c => c.ParentTaskId == parentTaskId)
            .SumAsync(c => c.CompletedQuantity, ct);

        await _context.ProjectTasks
            .Where(p => p.Id == parentTaskId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CompletedQuantity, total), ct);
    }

    // ⭐ Persist cha Done nguyên tử: chỉ set khi mọi con Done và cha chưa Done. Trả true nếu CHÍNH lệnh này chuyển.
    public async Task<bool> TrySetParentDoneAsync(
        int parentTaskId, DateTime completedAt, int completedByUserId, CancellationToken ct)
    {
        var affected = await _context.ProjectTasks
            .Where(p => p.Id == parentTaskId
                && p.Status != TaskItemStatus.Done
                && p.Children.Any()
                && !p.Children.Any(c => c.Status != TaskItemStatus.Done))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, TaskItemStatus.Done)
                .SetProperty(p => p.CompletedAt, completedAt)
                .SetProperty(p => p.CompletedByUserId, completedByUserId), ct);

        return affected == 1;
    }
}
