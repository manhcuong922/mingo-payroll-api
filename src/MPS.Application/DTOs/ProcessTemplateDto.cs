namespace MPS.Application.DTOs;

public record ProcessStepDto(
    int Id,
    string Name,
    string? Description,
    int StepOrder,
    string DefaultProgressType,   // "Quantity" → sinh công đoạn; "Milestone" → sinh gate
    int DefaultPlannedQuantity,
    int DefaultPlannedWorkUnits,
    bool DefaultIsRequiredGate,   // chỉ có ý nghĩa khi DefaultProgressType="Milestone"
    int? Priority);

public record ProcessTemplateDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    int? DefaultDepartmentId,
    string? DefaultDepartmentName,
    IReadOnlyList<ProcessStepDto> Steps);
