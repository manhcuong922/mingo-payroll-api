using MPS.Application.DTOs;

namespace MPS.Application.Features.ProcessTemplate;

internal static class ProcessTemplateMapper
{
    public static ProcessTemplateDto ToDto(MPS.Domain.Entities.ProcessTemplate t)
    {
        var steps = t.Steps
            .OrderBy(s => s.StepOrder)
            .Select(s => new ProcessStepDto(
                s.Id,
                s.Name,
                s.Description,
                s.StepOrder,
                s.DefaultProgressType.ToString(),
                s.DefaultWeight,
                s.Priority))
            .ToList();

        return new ProcessTemplateDto(
            t.Id,
            t.Code,
            t.Name,
            t.Description,
            t.IsActive,
            t.DefaultDepartmentId,
            t.DefaultDepartment?.Name,
            steps);
    }
}
