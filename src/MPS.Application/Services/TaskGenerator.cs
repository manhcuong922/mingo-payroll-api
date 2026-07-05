using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;

namespace MPS.Application.Services;

public sealed class TaskGenerator : ITaskGenerator
{
    public IReadOnlyList<ProjectTask> BuildStages(
        Project project,
        ProcessTemplate template,
        int createdByUserId,
        IReadOnlyList<GenerateStepOverride>? overrides)
    {
        var departmentId = project.DepartmentId ?? template.DefaultDepartmentId
            ?? throw new DomainValidationException("Đơn chưa gán xưởng.");

        var overrideMap = overrides?.ToDictionary(o => o.StepId) ?? new();

        // Mỗi bước → 1 task cha (công đoạn), theo StepOrder. Đều Todo, không assignee, không con.
        var stages = new List<ProjectTask>();
        foreach (var step in template.Steps.OrderBy(s => s.StepOrder))
        {
            overrideMap.TryGetValue(step.Id, out var ovr);
            var progressType = ovr?.ProgressTypeOverride ?? step.DefaultProgressType;

            stages.Add(new ProjectTask
            {
                Title = step.Name,
                StepOrder = step.StepOrder,
                Priority = step.Priority,
                IsBatchParent = true,
                Type = TaskItemType.ProcessTask,
                Status = TaskItemStatus.Todo,
                ProgressType = progressType,
                PlannedQuantity = progressType == ProgressType.Quantity
                    ? (ovr?.PlannedQuantityOverride ?? project.Quantity)
                    : 0,
                Weight = progressType == ProgressType.Milestone
                    ? (ovr?.WeightOverride ?? step.DefaultWeight)
                    : 0,
                Deadline = ovr?.DeadlineOverride,
                DepartmentId = departmentId,
                ProjectId = project.Id,
                CreatedByUserId = createdByUserId,
            });
        }

        return stages;
    }
}
