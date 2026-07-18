namespace MPS.Application.DTOs;

public record ProcessStepDto(
    int Id,
    int? ParentProcessStepId,
    string Name,
    string? Description,
    int StepOrder,
    string DefaultProgressType,   // "Quantity" (container/work-leaf) | "Milestone" (gate)
    int DefaultPlannedQuantity,
    int DefaultPlannedWorkUnits,
    bool DefaultIsRequiredGate,   // chỉ có ý nghĩa khi DefaultProgressType="Milestone"
    int PlannedHeadcount,
    int? RequiredPositionId,
    bool ScalesWithQuantity,
    double? DefaultDeadlineOffsetRatio,
    int? Priority,
    IReadOnlyList<ProcessStepDto> Children);

public record ProcessTemplateDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int BaseQuantity,
    int? DefaultDepartmentId,
    string? DefaultDepartmentName,
    IReadOnlyList<ProcessStepDto> Steps);   // cây: node top-level, con qua Children[]
