using MPS.Domain.Common;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;

namespace MPS.Application.Services;

public sealed class TaskGenerator : ITaskGenerator
{
    // Cap an toàn chống đệ quy vô hạn / cây bệnh lý (refactor mục N). Cũng validate khi soạn template.
    public const int MaxTreeDepth = 8;

    public IReadOnlyList<ProjectTask> BuildTree(Project project, ProcessTemplate template, int createdByUserId)
    {
        var departmentId = project.DepartmentId ?? template.DefaultDepartmentId
            ?? throw new DomainValidationException("Đơn chưa gán xưởng.");

        if (template.BaseQuantity <= 0)
            throw new DomainValidationException("BaseQuantity của quy trình phải > 0.");

        var factor = (double)project.Quantity / template.BaseQuantity;

        // template.Steps là list phẳng mọi node — dựng lookup con-theo-cha từ ParentProcessStepId.
        // Dùng 0 làm sentinel cho "root" (ProcessStep.Id luôn > 0) — Dictionary<int?,_> ném lỗi với key null.
        var childrenByParent = template.Steps
            .GroupBy(s => s.ParentProcessStepId ?? 0)
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.StepOrder).ThenBy(s => s.Id).ToList());

        var roots = childrenByParent.TryGetValue(0, out var topLevel) ? topLevel : new List<ProcessStep>();

        var built = new List<ProjectTask>();
        foreach (var step in roots)
            built.Add(BuildNode(step, parent: null, project, departmentId, createdByUserId, factor, childrenByParent, depth: 1));

        return Flatten(built);
    }

    private static ProjectTask BuildNode(
        ProcessStep step,
        ProjectTask? parent,
        Project project,
        int departmentId,
        int createdByUserId,
        double factor,
        IReadOnlyDictionary<int, List<ProcessStep>> childrenByParent,
        int depth)
    {
        if (depth > MaxTreeDepth)
            throw new DomainValidationException($"Cây quy trình vượt quá độ sâu tối đa ({MaxTreeDepth}).");

        var childSteps = childrenByParent.TryGetValue(step.Id, out var cs) ? cs : new List<ProcessStep>();
        var hasChildren = childSteps.Count > 0;
        var isGate = step.DefaultProgressType == ProgressType.Milestone;

        var node = new ProjectTask
        {
            Title = step.Name,
            Description = step.Description,
            StepOrder = step.StepOrder,
            Priority = step.Priority,
            Type = TaskItemType.ProcessTask,
            Status = TaskItemStatus.Todo,
            ProgressType = isGate ? ProgressType.Milestone : ProgressType.Quantity,
            IsBatchParent = hasChildren,
            PlannedWorkUnits = step.DefaultPlannedWorkUnits,   // trọng số/capacity — KHÔNG scale
            DepartmentId = departmentId,
            ProjectId = project.Id,
            CreatedByUserId = createdByUserId,
            ParentTask = parent,
            DeadlineOffsetRatio = step.DefaultDeadlineOffsetRatio,
            Deadline = TaskDeadlineCalculator.DeadlineFromRatio(
                project.StartDate, project.Deadline, step.DefaultDeadlineOffsetRatio),
        };

        if (isGate)
        {
            node.IsRequiredGate = step.DefaultIsRequiredGate;
            node.PlannedQuantity = 0;
        }
        else if (hasChildren)
        {
            // Container: không mang sản lượng/headcount trực tiếp (tiến độ = rollup con).
            node.PlannedQuantity = 0;
        }
        else
        {
            // Work-leaf: sản lượng scale (ceil), headcount + chức danh cố định (không scale).
            node.PlannedQuantity = step.ScalesWithQuantity
                ? (int)Math.Ceiling(step.DefaultPlannedQuantity * factor)
                : step.DefaultPlannedQuantity;
            node.PlannedHeadcount = step.PlannedHeadcount;
            node.RequiredPositionId = step.RequiredPositionId;
        }

        foreach (var childStep in childSteps)
        {
            var childNode = BuildNode(childStep, node, project, departmentId, createdByUserId, factor, childrenByParent, depth + 1);
            node.Children.Add(childNode);
        }

        return node;
    }

    private static List<ProjectTask> Flatten(IEnumerable<ProjectTask> roots)
    {
        var list = new List<ProjectTask>();

        void Walk(ProjectTask node)
        {
            list.Add(node);
            foreach (var child in node.Children) Walk(child);
        }

        foreach (var root in roots) Walk(root);
        return list;
    }
}
