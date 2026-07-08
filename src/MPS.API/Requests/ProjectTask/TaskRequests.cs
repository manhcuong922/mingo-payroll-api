using MPS.Domain.Enums;

namespace MPS.API.Requests.ProjectTask;

public record CreateTaskRequest(
    int? ProjectId,
    int DepartmentId,
    int? ParentTaskId,
    string Title,
    string? Description,
    ProgressType ProgressType,
    int PlannedQuantity,
    int PlannedWorkUnits,
    bool IsRequiredGate,
    DateTime? Deadline,
    int? Priority,
    int StepOrder,
    IReadOnlyList<int> AssigneeUserIds,
    bool IsBatchParent = false,
    int? AssignedPmUserId = null);

public record AssignPmRequest(int PmUserId);

public record CreateConfidentialTaskRequest(
    string Title,
    string? Description,
    IReadOnlyList<int> AssigneeUserIds,
    DateTime? Deadline,
    int? Priority);

public record UpdateTaskRequest(
    string Title,
    string? Description,
    DateTime? Deadline,
    int? Priority,
    int PlannedQuantity,
    int PlannedWorkUnits,
    bool IsRequiredGate,
    IReadOnlyList<int>? AssigneeUserIds);

public record UpdateTaskProgressRequest(
    int CompletedQuantity,
    string RowVersion);

public record CompleteTaskRequest(string RowVersion, DateTime? CompletedAt = null);
