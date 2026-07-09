using MediatR;
using MPS.Domain.Enums;

namespace MPS.Application.Features.ProcessTemplate.Commands;

// Node cây quy trình N tầng (refactor). Container = có Children; work-leaf Quantity = không con + qty/headcount/position;
// gate = Milestone. Ratio con ≤ ratio cha (validate ở TreeBuilder).
public record ProcessStepInput(
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
    IReadOnlyList<ProcessStepInput> Children);

public record ProcessTemplateCreateCommand(
    string Code,
    string Name,
    string? Description,
    int BaseQuantity,
    int? DefaultDepartmentId,
    IReadOnlyList<ProcessStepInput> Steps) : IRequest<int>;
