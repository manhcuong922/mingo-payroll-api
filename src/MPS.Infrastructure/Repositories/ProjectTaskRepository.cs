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

    public async Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(
        int projectId, CancellationToken ct, bool includeCancelled = false)
    {
        var query = _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Where(t => t.ProjectId == projectId)
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(t => t.DeletedAt == null);

        return await query.OrderBy(t => t.StepOrder).ThenBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectTask>> GetByProjectIdsAsync(
        IReadOnlyCollection<int> projectIds, CancellationToken ct, bool includeCancelled = false)
    {
        if (projectIds.Count == 0) return Array.Empty<ProjectTask>();

        var query = _context.ProjectTasks
            .AsNoTracking()
            .Where(t => t.ProjectId != null && projectIds.Contains(t.ProjectId.Value))
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(t => t.DeletedAt == null);

        return await query
            .OrderBy(t => t.ProjectId)
            .ThenBy(t => t.StepOrder)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(
        int departmentId, CancellationToken ct, bool includeCancelled = false)
    {
        var query = _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Assignees).ThenInclude(a => a.User)
            .Where(t => t.DepartmentId == departmentId)
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(t => t.DeletedAt == null);

        return await query.OrderBy(t => t.StepOrder).ThenBy(t => t.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(
        int userId, CancellationToken ct, bool includeCancelled = false)
    {
        var query = _context.ProjectTasks
            .AsNoTracking()
            .Include(t => t.Department)
            .Include(t => t.Assignees)
            .Where(t => !t.IsBatchParent && t.Assignees.Any(a => a.UserId == userId))
            .AsQueryable();

        if (!includeCancelled)
            query = query.Where(t => t.DeletedAt == null);

        return await query.OrderBy(t => t.Deadline).ToListAsync(ct);
    }

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

    // Chỉ tính con Quantity — Gate (Milestone) không có sản lượng, không tham gia giới hạn PlannedQuantity cha.
    public Task<int> SumChildrenPlannedQuantityAsync(int parentTaskId, int? excludeTaskId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(c => c.ParentTaskId == parentTaskId && c.Id != (excludeTaskId ?? 0)
                && c.ProgressType == ProgressType.Quantity)
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

    // ⭐ Cha tự cộng: CompletedQuantity của cha = SUM(CompletedQuantity các con Quantity). Gate (Milestone)
    // không đóng góp — luôn giữ CompletedQuantity=0 (xem TaskCompleteCommandHandler).
    public async Task RecomputeParentCompletedAsync(int parentTaskId, CancellationToken ct)
    {
        var total = await _context.ProjectTasks
            .Where(c => c.ParentTaskId == parentTaskId && c.ProgressType == ProgressType.Quantity)
            .SumAsync(c => c.CompletedQuantity, ct);

        await _context.ProjectTasks
            .Where(p => p.Id == parentTaskId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.CompletedQuantity, total), ct);
    }

    // ⭐ Persist cha Done nguyên tử — điều kiện đúng docs/plans/plan.md mục 4.3, cả 3 phải thoả:
    //   1) Sản lượng luỹ kế đã đạt kế hoạch (tránh PM chỉ chia việc một phần sản lượng — vd kế hoạch 500
    //      nhưng mới giao 300 — rồi các task con đó Done hết làm cha bị đánh Done sai dù còn thiếu sản lượng).
    //   2) MỌI task con Quantity đã Done (không chỉ đủ tổng số — task Quantity lẻ tẻ còn dang dở vẫn chặn).
    //   3) MỌI Gate BẮT BUỘC (Milestone, IsRequiredGate=true) đã Done — Gate tuỳ chọn chưa Done không chặn.
    public async Task<bool> TrySetParentDoneAsync(
        int parentTaskId, DateTime completedAt, int completedByUserId, CancellationToken ct)
    {
        var affected = await _context.ProjectTasks
            .Where(p => p.Id == parentTaskId
                && p.Status != TaskItemStatus.Done
                && p.PlannedQuantity > 0
                && p.CompletedQuantity >= p.PlannedQuantity
                && !p.Children.Any(c => c.ProgressType == ProgressType.Quantity && c.Status != TaskItemStatus.Done)
                && !p.Children.Any(c => c.ProgressType == ProgressType.Milestone
                    && c.IsRequiredGate && c.Status != TaskItemStatus.Done))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Status, TaskItemStatus.Done)
                .SetProperty(p => p.CompletedAt, completedAt)
                .SetProperty(p => p.CompletedByUserId, completedByUserId), ct);

        return affected == 1;
    }

    // ⭐ Pause lan truyền: con trực tiếp của stageId lưu Status hiện tại vào StatusBeforePause rồi chuyển Paused,
    // đánh dấu PausedByCascadeFromId=stageId ("bị lan truyền" — không tự Resume độc lập, plan mục 6.5).
    public Task CascadePauseChildrenAsync(int stageId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(c => c.ParentTaskId == stageId && c.Status != TaskItemStatus.Paused && c.Status != TaskItemStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.StatusBeforePause, c => c.Status)
                .SetProperty(c => c.Status, TaskItemStatus.Paused)
                .SetProperty(c => c.PausedByCascadeFromId, stageId), ct);

    // ⭐ Resume lan truyền: chỉ con đang Paused DO lan truyền TỪ ĐÚNG stageId này mới được trả lại status cũ.
    public Task CascadeResumeChildrenAsync(int stageId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(c => c.ParentTaskId == stageId && c.PausedByCascadeFromId == stageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, c => c.StatusBeforePause ?? TaskItemStatus.Todo)
                .SetProperty(c => c.StatusBeforePause, (TaskItemStatus?)null)
                .SetProperty(c => c.PausedByCascadeFromId, (int?)null), ct);

    // ⭐ Cancel lan truyền: con trực tiếp soft-delete + lưu status cũ (khác Pause: dùng DeletedAt thay StatusBeforePause riêng
    // — dùng lại cùng field StatusBeforePause để Restore biết trả lại đúng trạng thái).
    public Task CascadeCancelChildrenAsync(int stageId, DateTime deletedAt, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(c => c.ParentTaskId == stageId && c.Status != TaskItemStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.StatusBeforePause, c => c.Status)
                .SetProperty(c => c.Status, TaskItemStatus.Cancelled)
                .SetProperty(c => c.PausedByCascadeFromId, stageId)
                .SetProperty(c => c.DeletedAt, deletedAt), ct);

    // ⭐ Restore lan truyền: chỉ con bị Cancel DO lan truyền TỪ ĐÚNG stageId này mới được khôi phục theo cha.
    public Task CascadeRestoreChildrenAsync(int stageId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(c => c.ParentTaskId == stageId && c.PausedByCascadeFromId == stageId && c.Status == TaskItemStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, c => c.StatusBeforePause ?? TaskItemStatus.Todo)
                .SetProperty(c => c.StatusBeforePause, (TaskItemStatus?)null)
                .SetProperty(c => c.PausedByCascadeFromId, (int?)null)
                .SetProperty(c => c.DeletedAt, (DateTime?)null), ct);

    // Đơn (Tầng 1) không có bảng ProjectTask riêng để trỏ Id vào PausedByCascadeFromId — dùng -projectId
    // (số ÂM) để phân biệt "lan truyền từ Đơn" với "lan truyền từ Công đoạn" (luôn dương, chính ProjectTask.Id).
    private static int ProjectCascadeMarker(int projectId) => -projectId;

    public Task CascadePauseByProjectAsync(int projectId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && t.Status != TaskItemStatus.Paused && t.Status != TaskItemStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.StatusBeforePause, t => t.Status)
                .SetProperty(t => t.Status, TaskItemStatus.Paused)
                .SetProperty(t => t.PausedByCascadeFromId, ProjectCascadeMarker(projectId)), ct);

    public Task CascadeResumeByProjectAsync(int projectId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && t.PausedByCascadeFromId == ProjectCascadeMarker(projectId))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, t => t.StatusBeforePause ?? TaskItemStatus.Todo)
                .SetProperty(t => t.StatusBeforePause, (TaskItemStatus?)null)
                .SetProperty(t => t.PausedByCascadeFromId, (int?)null), ct);

    public Task CascadeCancelByProjectAsync(int projectId, DateTime deletedAt, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && t.Status != TaskItemStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.StatusBeforePause, t => t.Status)
                .SetProperty(t => t.Status, TaskItemStatus.Cancelled)
                .SetProperty(t => t.PausedByCascadeFromId, ProjectCascadeMarker(projectId))
                .SetProperty(t => t.DeletedAt, deletedAt), ct);

    public Task CascadeRestoreByProjectAsync(int projectId, CancellationToken ct) =>
        _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && t.PausedByCascadeFromId == ProjectCascadeMarker(projectId)
                && t.Status == TaskItemStatus.Cancelled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, t => t.StatusBeforePause ?? TaskItemStatus.Todo)
                .SetProperty(t => t.StatusBeforePause, (TaskItemStatus?)null)
                .SetProperty(t => t.PausedByCascadeFromId, (int?)null)
                .SetProperty(t => t.DeletedAt, (DateTime?)null), ct);
}
