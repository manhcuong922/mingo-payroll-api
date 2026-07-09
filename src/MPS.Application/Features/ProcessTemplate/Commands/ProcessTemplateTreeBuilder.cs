using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.ProcessTemplate.Commands;

/// <summary>
/// Dựng cây ProcessStep N tầng từ input lồng nhau + validate bất biến (refactor mục 1/4/10):
///   - container (có con): DefaultProgressType phải Quantity; không mang sản lượng/headcount/position trực tiếp;
///   - work-leaf (không con, Quantity): DefaultPlannedQuantity&gt;0, PlannedHeadcount≥1, RequiredPositionId&gt;0;
///   - gate (Milestone): không con, sản lượng = 0;
///   - ratio 0..1 và ratio con ≤ ratio cha (deadline con ≤ cha);
///   - cap độ sâu MaxTreeDepth.
/// Trả về LIST PHẲNG mọi node (đã wire Parent nav) để gán vào ProcessTemplate.Steps — EF set cả
/// ProcessTemplateId (qua Steps nav) lẫn ParentProcessStepId (qua Parent nav) khi save.
/// </summary>
internal static class ProcessTemplateTreeBuilder
{
    public static List<ProcessStep> Build(IReadOnlyList<ProcessStepInput> roots)
    {
        if (roots is null || roots.Count == 0)
            throw new DomainValidationException("Quy trình phải có ít nhất 1 node.");

        var flat = new List<ProcessStep>();
        var seenOrders = new HashSet<int>();
        foreach (var input in roots)
        {
            if (!seenOrders.Add(input.StepOrder))
                throw new DomainValidationException($"Trùng StepOrder {input.StepOrder} ở node top-level.");
            BuildNode(input, parent: null, parentRatio: 1.0, depth: 1, flat);
        }
        return flat;
    }

    private static ProcessStep BuildNode(
        ProcessStepInput input, ProcessStep? parent, double parentRatio, int depth, List<ProcessStep> flat)
    {
        if (depth > TaskGenerator.MaxTreeDepth)
            throw new DomainValidationException($"Cây quy trình vượt quá độ sâu tối đa ({TaskGenerator.MaxTreeDepth}).");

        if (string.IsNullOrWhiteSpace(input.Name))
            throw new DomainValidationException("Tên node không được rỗng.");

        var hasChildren = input.Children is { Count: > 0 };
        var isGate = input.DefaultProgressType == ProgressType.Milestone;

        if (input.DefaultDeadlineOffsetRatio is { } ratio)
        {
            if (ratio < 0 || ratio > 1)
                throw new DomainValidationException($"DeadlineOffsetRatio node '{input.Name}' phải trong [0,1].");
            if (ratio > parentRatio + 1e-9)
                throw new DomainValidationException(
                    $"DeadlineOffsetRatio node '{input.Name}' ({ratio}) không được vượt của node cha ({parentRatio}).");
        }

        if (isGate && hasChildren)
            throw new DomainValidationException($"Gate '{input.Name}' không được có node con.");

        var node = new ProcessStep
        {
            Name = input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            StepOrder = input.StepOrder,
            DefaultProgressType = isGate ? ProgressType.Milestone : ProgressType.Quantity,
            DefaultPlannedWorkUnits = input.DefaultPlannedWorkUnits,
            DefaultDeadlineOffsetRatio = input.DefaultDeadlineOffsetRatio,
            Priority = input.Priority,
            Parent = parent,
        };

        if (isGate)
        {
            node.DefaultIsRequiredGate = input.DefaultIsRequiredGate;
            node.DefaultPlannedQuantity = 0;
            node.ScalesWithQuantity = false;
        }
        else if (hasChildren)
        {
            // Container: không sản lượng/headcount/position trực tiếp.
            node.DefaultPlannedQuantity = 0;
            node.PlannedHeadcount = 0;
            node.RequiredPositionId = null;
            node.ScalesWithQuantity = false;
        }
        else
        {
            // Work-leaf Quantity: bắt buộc sản lượng + headcount + chức danh.
            if (input.DefaultPlannedQuantity <= 0)
                throw new DomainValidationException($"Vị trí lá '{input.Name}' phải có sản lượng > 0.");
            if (input.PlannedHeadcount < 1)
                throw new DomainValidationException($"Vị trí lá '{input.Name}' phải có số người ≥ 1.");
            if (input.RequiredPositionId is not > 0)
                throw new DomainValidationException($"Vị trí lá '{input.Name}' phải chỉ định chức danh (RequiredPositionId).");

            node.DefaultPlannedQuantity = input.DefaultPlannedQuantity;
            node.PlannedHeadcount = input.PlannedHeadcount;
            node.RequiredPositionId = input.RequiredPositionId;
            node.ScalesWithQuantity = input.ScalesWithQuantity;
        }

        flat.Add(node);

        if (hasChildren)
        {
            var childRatio = input.DefaultDeadlineOffsetRatio ?? parentRatio;
            var seenOrders = new HashSet<int>();
            foreach (var child in input.Children!)
            {
                if (!seenOrders.Add(child.StepOrder))
                    throw new DomainValidationException(
                        $"Trùng StepOrder {child.StepOrder} trong các con của '{input.Name}'.");

                var childNode = BuildNode(child, node, childRatio, depth + 1, flat);
                node.Children.Add(childNode);
            }
        }

        return node;
    }
}
