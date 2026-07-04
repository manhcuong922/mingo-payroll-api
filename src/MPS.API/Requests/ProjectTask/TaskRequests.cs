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
    int Weight,
    bool RequiresHandover,
    DateTime? Deadline,
    int? Priority,
    int StepOrder,
    IReadOnlyList<int> AssigneeUserIds);

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
    int Weight,
    IReadOnlyList<int>? AssigneeUserIds);

public record UpdateTaskProgressRequest(
    int CompletedQuantity,
    string RowVersion);

public record CompleteTaskRequest(string RowVersion);
