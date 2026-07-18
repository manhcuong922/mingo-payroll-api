using MPS.Domain.Entities;

namespace MPS.Application.Services;

public interface ITaskGenerator
{
    /// <summary>
    /// Sinh FULL cây task N tầng cho một đơn từ cây node của ProcessTemplate (refactor task-full-generation):
    ///   - container (ProcessStep có con) → node gộp (IsBatchParent=true), không sản lượng/không gán.
    ///   - work-leaf (không con, Quantity) → vị trí gán được: PlannedQuantity = ceil(scale), PlannedHeadcount,
    ///     RequiredPositionId, DeadlineOffsetRatio. 0 assignee — Planning gán sau.
    ///   - gate (Milestone) ở bất kỳ tầng → node chặn (IsRequiredGate), không sản lượng, không %.
    /// Scale sản lượng: factor = Project.Quantity / Template.BaseQuantity, ceil mỗi lá có ScalesWithQuantity=true.
    /// PlannedWorkUnits (trọng số) &amp; PlannedHeadcount KHÔNG scale. Deadline mỗi node = ratio × timeline đơn.
    /// </summary>
    IReadOnlyList<ProjectTask> BuildTree(Project project, ProcessTemplate template, int createdByUserId);
}
