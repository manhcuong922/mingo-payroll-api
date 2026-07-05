namespace MPS.Application.DTOs;

public record ProcessStepDto(
    int Id,
    string Name,
    string? Description,
    int StepOrder,
    string DefaultProgressType,   // "Quantity" | "Milestone"
    int DefaultWeight,
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
