using MediatR;

namespace MPS.Application.Features.ProcessTemplate.Commands;

public record ProcessTemplateUpdateCommand(
    int Id,
    string Name,
    string? Description,
    bool IsActive,
    int BaseQuantity,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepInput> Steps) : IRequest<bool>;
