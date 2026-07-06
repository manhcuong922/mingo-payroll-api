using MPS.Domain.Enums;

namespace MPS.API.Requests.ProcessTemplate;

public record ProcessStepRequest(
    string Name,
    string? Description,
    int StepOrder,
    ProgressType DefaultProgressType,
    int DefaultWeight,
    int? Priority);

public record CreateProcessTemplateRequest(
    string Code,
    string Name,
    string? Description,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepRequest> Steps);

public record UpdateProcessTemplateRequest(
    string Name,
    string? Description,
    bool IsActive,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepRequest> Steps);
