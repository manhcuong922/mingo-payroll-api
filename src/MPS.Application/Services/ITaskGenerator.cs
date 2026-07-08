using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

/// <summary>Override số liệu 1 bước khi áp quy trình mẫu (không bắt buộc dùng đúng số mẫu).</summary>
public record GenerateStepOverride(
    int StepId,
    int? PlannedQuantityOverride,
    int? PlannedWorkUnitsOverride,
    DateTime? DeadlineOverride,
    ProgressType? ProgressTypeOverride,
    int? AssignedPmUserId = null);

public interface ITaskGenerator
{
    /// <summary>
    /// Sinh task từ ProcessTemplate cho một đơn: step Quantity → công đoạn (IsBatchParent=true);
    /// step Milestone/Gate → gate trực tiếp dưới Project (IsBatchParent=false, ParentTaskId=null).
    /// KHÔNG auto-chia con cho công đoạn, KHÔNG auto-assign — quản lý xưởng chia sau bằng
    /// CreateTask(ParentTaskId=cha).
    /// </summary>
    IReadOnlyList<ProjectTask> BuildStages(
        Project project,
        ProcessTemplate template,
        int createdByUserId,
        IReadOnlyList<GenerateStepOverride>? overrides);
}
