using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdAsync(int id, CancellationToken ct);
    Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct);       // include Assignees + Children + Attachments
    // includeCancelled=false (mặc định, plan mục 6.5): ẩn task đã Cancelled (DeletedAt != null) khỏi danh sách.
    Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct, bool includeCancelled = false);
    Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(int departmentId, CancellationToken ct, bool includeCancelled = false);
    Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct, bool includeCancelled = false);  // qua TaskAssignee

    Task<bool> HasChildrenAsync(int taskId, CancellationToken ct);
    Task<bool> AllChildrenDoneAsync(int parentTaskId, CancellationToken ct);      // cho persist cha Done
    Task<int> CountActiveByUserAsync(int userId, CancellationToken ct);           // gợi ý tải khi chia việc

    // ⭐ Tổng sản lượng quy đổi các con hiện có của 1 công đoạn (không tính task đang sửa, nếu có) —
    // con Quantity góp PlannedQuantity, con Milestone góp Weight (Weight = sản lượng quy đổi khi Done).
    Task<int> SumChildrenPlannedQuantityAsync(int parentTaskId, int? excludeTaskId, CancellationToken ct);

    Task<int> AddAsync(ProjectTask entity, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<ProjectTask> entities, CancellationToken ct);  // sinh loạt công đoạn từ template
    Task<bool> UpdateAsync(ProjectTask entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);

    // Update có kiểm optimistic concurrency: expectedRowVersion khớp mới ghi.
    // Trả false nếu RowVersion đã đổi (client cần reload) — handler ánh xạ sang 409.
    Task<bool> UpdateWithConcurrencyAsync(ProjectTask entity, byte[] expectedRowVersion, CancellationToken ct);

    // ⭐ Cha tự cộng: CompletedQuantity của cha = SUM(CompletedQuantity các con).
    Task RecomputeParentCompletedAsync(int parentTaskId, CancellationToken ct);

    // ⭐ Persist cha Done bằng UPDATE có điều kiện nguyên tử: chỉ set khi mọi con đã Done.
    // Trả true nếu CHÍNH lệnh này chuyển cha sang Done (1 hàng ảnh hưởng) — để chạy side-effect đúng 1 lần.
    Task<bool> TrySetParentDoneAsync(int parentTaskId, DateTime completedAt, int completedByUserId, CancellationToken ct);

    // ⭐ Pause/Cancel lan truyền theo tầng (plan mục 6.5). Con trực tiếp của stageId chuyển Paused/Cancelled,
    // lưu StatusBeforePause (trạng thái cũ) + PausedByCascadeFromId=stageId ("bị lan truyền", không tự Resume/Restore).
    Task CascadePauseChildrenAsync(int stageId, CancellationToken ct);
    Task CascadeResumeChildrenAsync(int stageId, CancellationToken ct);
    Task CascadeCancelChildrenAsync(int stageId, DateTime deletedAt, CancellationToken ct);
    Task CascadeRestoreChildrenAsync(int stageId, CancellationToken ct);

    // ⭐ Đơn (Tầng 1) Pause/Cancel → lan truyền xuống TOÀN BỘ Công đoạn + Task con của projectId
    // (mọi ProjectTask.ProjectId == projectId, không chỉ con trực tiếp — cả cây 2 tầng dưới, plan mục 6.5).
    Task CascadePauseByProjectAsync(int projectId, CancellationToken ct);
    Task CascadeResumeByProjectAsync(int projectId, CancellationToken ct);
    Task CascadeCancelByProjectAsync(int projectId, DateTime deletedAt, CancellationToken ct);
    Task CascadeRestoreByProjectAsync(int projectId, CancellationToken ct);
}
