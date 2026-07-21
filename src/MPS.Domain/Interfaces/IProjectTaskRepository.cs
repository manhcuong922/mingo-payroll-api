using MPS.Domain.Entities;

namespace MPS.Domain.Interfaces;

public interface IProjectTaskRepository
{
    Task<ProjectTask?> GetByIdAsync(int id, CancellationToken ct);
    Task<ProjectTask?> GetByIdWithGraphAsync(int id, CancellationToken ct);       // include Assignees + Children + Attachments

    // ⭐ #20: bản TRACKED (không AsNoTracking, không Include collection) để AuditSaveChangesInterceptor
    // so được OriginalValue/CurrentValue và ghi được diff trước–sau. Dùng cho đường GHI.
    Task<ProjectTask?> GetByIdForUpdateAsync(int id, CancellationToken ct);

    // Danh sách userId đang được gán vào task (đọc rời, tránh phải Include collection ở đường ghi).
    Task<IReadOnlyList<int>> GetAssigneeUserIdsAsync(int taskId, CancellationToken ct);

    // Lưu entity đang được change-tracker theo dõi. Khác UpdateAsync: KHÔNG gọi DbSet.Update
    // (giữ nguyên OriginalValue để audit ghi đúng diff) và luôn trả true khi entity tồn tại
    // — SaveChanges trả 0 khi payload trùng dữ liệu cũ là hợp lệ, không phải "not found".
    Task<bool> SaveTrackedAsync(CancellationToken ct);
    // includeCancelled=false (mặc định, plan mục 6.5): ẩn task đã Cancelled (DeletedAt != null) khỏi danh sách.
    Task<IReadOnlyList<ProjectTask>> GetByProjectIdAsync(int projectId, CancellationToken ct, bool includeCancelled = false);
    Task<IReadOnlyList<ProjectTask>> GetByProjectIdsAsync(IReadOnlyCollection<int> projectIds, CancellationToken ct, bool includeCancelled = false);
    Task<IReadOnlyList<ProjectTask>> GetByDepartmentAsync(int departmentId, CancellationToken ct, bool includeCancelled = false);
    Task<IReadOnlyList<ProjectTask>> GetAssignedToUserAsync(int userId, CancellationToken ct, bool includeCancelled = false);  // qua TaskAssignee

    Task<bool> HasChildrenAsync(int taskId, CancellationToken ct);
    Task<bool> HasAnyByProjectAsync(int projectId, CancellationToken ct);        // idempotency guard cho generate

    /// <summary>
    /// #16 — sinh cây task nguyên tử: mở transaction + khoá theo khoá nghiệp vụ "project:{id}" rồi mới
    /// check-then-insert. Không có khoá thì 2 request song song cùng thấy "chưa có task" và sinh 2 cây
    /// chồng nhau (số task gấp đôi). <paramref name="buildTasks"/> chạy BÊN TRONG khoá; trả về null để
    /// huỷ (đã có task) — khi đó hàm trả về null và caller ném lỗi nghiệp vụ.
    /// </summary>
    Task<int?> GenerateExclusiveAsync(
        int projectId,
        Func<CancellationToken, Task<IReadOnlyList<ProjectTask>?>> buildTasks,
        CancellationToken ct);
    Task<bool> AllChildrenDoneAsync(int parentTaskId, CancellationToken ct);      // cho persist cha Done
    Task<int> CountActiveByUserAsync(int userId, CancellationToken ct);           // gợi ý tải khi chia việc
    // ⭐ refactor D: chỉ chặn gán khi người đó đang có task InProgress (nới luật "1 task mở").
    Task<int> CountInProgressByUserAsync(int userId, CancellationToken ct);

    // ⭐ #24 (PO chốt 2026-07-21): chặn giao việc chồng — đếm task lá CHƯA XONG (Todo/InProgress)
    // mà người này đang giữ. Paused/Cancelled/Done không tính.
    Task<int> CountOpenByUserAsync(int userId, CancellationToken ct);

    // ⭐ #28: các task lá chưa Done mà NV đang đứng tên — dùng để cảnh báo khi chấm dứt HĐ.
    Task<IReadOnlyList<ProjectTask>> GetOpenAssignedTasksByUserAsync(int userId, CancellationToken ct);

    // ⭐ OT workflow v3 (Q18): check NV được assign task nào trong 1 project cụ thể.
    // Không lọc Status task — miễn có TaskAssignee row + task lá + task chưa soft-delete.
    // Dùng ở CreateOvertimeRequestCommand.
    Task<bool> HasAssignedTaskInProjectAsync(int userId, int projectId, CancellationToken ct);

    // ⭐ OT workflow v3: list task NV được assign trong 1 project — dùng ở
    // GetMyActiveOrdersForOtQuery để enrich context. Không lọc Status.
    Task<IReadOnlyList<ProjectTask>> GetAssignedTasksByUserInProjectAsync(int userId, int projectId, CancellationToken ct);

    // ⭐ OT workflow v3: distinct list ProjectId mà NV được assign task (bất kỳ Status).
    // Dùng ở GetMyActiveOrdersForOtQuery — auto-derive đơn cho NV form đăng ký.
    Task<IReadOnlyList<int>> GetAssignedProjectIdsByUserAsync(int userId, CancellationToken ct);

    // ⭐ Tổng PlannedQuantity các con Quantity hiện có của 1 công đoạn (không tính task đang sửa, nếu có) —
    // Gate (Milestone) không có sản lượng nên không tham gia.
    Task<int> SumChildrenPlannedQuantityAsync(int parentTaskId, int? excludeTaskId, CancellationToken ct);

    Task<int> AddAsync(ProjectTask entity, CancellationToken ct);
    Task AddRangeAsync(IEnumerable<ProjectTask> entities, CancellationToken ct);  // sinh loạt công đoạn từ template
    Task<bool> UpdateAsync(ProjectTask entity, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);

    // ⭐ Đồng bộ danh sách người gán của 1 task lá: xoá sạch dòng cũ rồi chèn mới. Tách riêng khỏi UpdateAsync
    // vì TaskAssignee dùng KHÓA GHÉP (TaskId, UserId) — EF Update coi entity mới là Modified → UPDATE 0 dòng.
    Task ReplaceAssigneesAsync(int taskId, IReadOnlyCollection<int> userIds, DateTime assignedAt, CancellationToken ct);

    // Update có kiểm optimistic concurrency: expectedRowVersion khớp mới ghi.
    // Trả false nếu RowVersion đã đổi (client cần reload) — handler ánh xạ sang 409.
    Task<bool> UpdateWithConcurrencyAsync(ProjectTask entity, byte[] expectedRowVersion, CancellationToken ct);

    // ⭐ Cha tự cộng: CompletedQuantity của cha = SUM(CompletedQuantity các con).
    Task RecomputeParentCompletedAsync(int parentTaskId, CancellationToken ct);

    // ⭐ Cascade thời gian (refactor TM): Planning sửa StartDate/Deadline đơn → recompute Deadline mọi task
    // có DeadlineOffsetRatio theo timeline mới (co giãn tỉ lệ). Đơn không Deadline ⇒ task có ratio về null.
    Task RecomputeDeadlinesFromRatioAsync(int projectId, DateOnly startDate, DateTime? deadline, CancellationToken ct);

    // ⭐ Rollup Done đệ quy lên tổ tiên (refactor): sau khi 1 leaf Done, đi ngược ParentTaskId — mỗi container
    // tự cộng CompletedQuantity + tự Done khi mọi con Done + gate bắt buộc Done + đạt capacity.
    Task RollupAncestorsAsync(int leafTaskId, DateTime completedAt, int completedByUserId, CancellationToken ct);

    // ⭐ Chỉ cập nhật CompletedQuantity luỹ kế lên mọi tổ tiên (không đổi Status) — dùng khi UpdateProgress leaf.
    Task RecomputeAncestorsCompletedAsync(int leafTaskId, CancellationToken ct);

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
