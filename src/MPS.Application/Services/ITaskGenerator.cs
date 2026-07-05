using MPS.Domain.Entities;
using MPS.Domain.Enums;

namespace MPS.Application.Services;

/// <summary>Override số liệu 1 bước khi áp quy trình mẫu (không bắt buộc dùng đúng số mẫu).</summary>
public record GenerateStepOverride(
    int StepId,
    int? PlannedQuantityOverride,
    int? WeightOverride,
    DateTime? DeadlineOverride,
    ProgressType? ProgressTypeOverride);

public interface ITaskGenerator
{
    /// <summary>
    /// Sinh các task CHA (công đoạn, IsBatchParent) từ ProcessTemplate cho một đơn.
    /// KHÔNG auto-chia con, KHÔNG auto-assign — quản lý xưởng chia sau bằng CreateTask(ParentTaskId=cha).
    /// </summary>
    IReadOnlyList<ProjectTask> BuildStages(
        Project project,
        ProcessTemplate template,
        int createdByUserId,
        IReadOnlyList<GenerateStepOverride>? overrides);
}
