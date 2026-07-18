using MPS.Application.DTOs;
using MPS.Domain.Entities;

namespace MPS.Application.Features.ProcessTemplate;

internal static class ProcessTemplateMapper
{
    public static ProcessTemplateDto ToDto(MPS.Domain.Entities.ProcessTemplate t)
    {
        // template.Steps là list phẳng mọi node — dựng cây theo ParentProcessStepId (0 = root sentinel).
        var childrenByParent = t.Steps
            .GroupBy(s => s.ParentProcessStepId ?? 0)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StepOrder).ThenBy(s => s.Id).ToList());

        var roots = childrenByParent.TryGetValue(0, out var top) ? top : new List<ProcessStep>();
        var steps = roots.Select(s => ToStepDto(s, childrenByParent)).ToList();

        return new ProcessTemplateDto(
            t.Id,
            t.Code,
            t.Name,
            t.Description,
            t.IsActive,
            t.BaseQuantity,
            t.DefaultDepartmentId,
            t.DefaultDepartment?.Name,
            steps);
    }

    private static ProcessStepDto ToStepDto(
        ProcessStep s, IReadOnlyDictionary<int, List<ProcessStep>> childrenByParent)
    {
        var children = childrenByParent.TryGetValue(s.Id, out var cs)
            ? cs.Select(c => ToStepDto(c, childrenByParent)).ToList()
            : new List<ProcessStepDto>();

        return new ProcessStepDto(
            s.Id,
            s.ParentProcessStepId,
            s.Name,
            s.Description,
            s.StepOrder,
            s.DefaultProgressType.ToString(),
            s.DefaultPlannedQuantity,
            s.DefaultPlannedWorkUnits,
            s.DefaultIsRequiredGate,
            s.PlannedHeadcount,
            s.RequiredPositionId,
            s.ScalesWithQuantity,
            s.DefaultDeadlineOffsetRatio,
            s.Priority,
            children);
    }
}
