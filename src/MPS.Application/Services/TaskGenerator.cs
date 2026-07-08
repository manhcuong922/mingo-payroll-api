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

        // Mỗi bước → 1 task theo StepOrder, đều Todo, không con. Step Quantity → công đoạn
        // (IsBatchParent=true, chưa chia assignee). Step Milestone/Gate → gate trực tiếp dưới Project
        // (IsBatchParent=false, ParentTaskId=null, không có sản lượng/khối lượng công việc).
        var stages = new List<ProjectTask>();
        foreach (var step in template.Steps.OrderBy(s => s.StepOrder))
        {
            overrideMap.TryGetValue(step.Id, out var ovr);
            var progressType = ovr?.ProgressTypeOverride ?? step.DefaultProgressType;
            var isGate = progressType == ProgressType.Milestone;

            stages.Add(new ProjectTask
            {
                Title = step.Name,
                StepOrder = step.StepOrder,
                Priority = step.Priority,
                IsBatchParent = !isGate,
                Type = TaskItemType.ProcessTask,
                Status = TaskItemStatus.Todo,
                ProgressType = progressType,
                PlannedQuantity = isGate ? 0 : ovr?.PlannedQuantityOverride ?? step.DefaultPlannedQuantity,
                PlannedWorkUnits = isGate ? 0 : ovr?.PlannedWorkUnitsOverride ?? step.DefaultPlannedWorkUnits,
                IsRequiredGate = isGate && step.DefaultIsRequiredGate,
                Deadline = ovr?.DeadlineOverride,
                DepartmentId = departmentId,
                ProjectId = project.Id,
                CreatedByUserId = createdByUserId,
                AssignedPmUserId = isGate ? null : ovr?.AssignedPmUserId,
            });
        }

        return stages;
    }
}
