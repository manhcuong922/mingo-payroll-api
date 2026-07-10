using MPS.Domain.Enums;

namespace MPS.API.Requests.ProcessTemplate;

// Node cây quy trình N tầng. Container = có Children[]; work-leaf Quantity = không con; gate = Milestone.
public record ProcessStepRequest(
    string Name,
    string? Description,
    int StepOrder,
    ProgressType DefaultProgressType,
    int DefaultPlannedQuantity,
    int DefaultPlannedWorkUnits,
    bool DefaultIsRequiredGate,
    int PlannedHeadcount,
    int? RequiredPositionId,
    bool ScalesWithQuantity,
    double? DefaultDeadlineOffsetRatio,
    int? Priority,
    IReadOnlyList<ProcessStepRequest>? Children);

public record CreateProcessTemplateRequest(
    string Code,
    string Name,
    string? Description,
    int BaseQuantity,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepRequest> Steps);

public record UpdateProcessTemplateRequest(
    string Name,
    string? Description,
    bool IsActive,
    int BaseQuantity,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepRequest> Steps);
