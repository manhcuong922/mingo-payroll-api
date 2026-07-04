using MediatR;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProcessTemplate.Commands;

public record ProcessStepInput(
    string Name,
    string? Description,
    int StepOrder,
    ProgressType DefaultProgressType,
    int DefaultWeight,
    int? Priority);

public record ProcessTemplateCreateCommand(
    string Code,
    string Name,
    string? Description,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepInput> Steps) : IRequest<int>;
