using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdAsync(int id, CancellationToken ct);
    Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct);       // include Assignees + Children + Attachments
    Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(int departmentId, CancellationToken ct);
    Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct);  // qua TaskAssignee

    Task<bool> HasChildrenAsync(int taskId, CancellationToken ct);
    Task<bool> AllChildrenDoneAsync(int parentTaskId, CancellationToken ct);      // cho persist cha Done
    Task<int> CountActiveByUserAsync(int userId, CancellationToken ct);           // gợi ý tải khi chia việc

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
}
