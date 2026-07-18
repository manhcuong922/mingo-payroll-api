using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Exceptions;

namespace MPS.Application.Features.ProcessTemplate.Commands;

/// <summary>
/// Dựng cây ProcessStep N tầng từ input lồng nhau + validate bất biến (refactor mục 1/4/10):
///   - container (có con): DefaultProgressType phải Quantity; không mang sản lượng/headcount/position trực tiếp;
///     bắt buộc DefaultPlannedWorkUnits &gt; 0 (là % đóng góp của nhóm vào nhóm cha);
///   - work-leaf (không con, Quantity): DefaultPlannedQuantity&gt;0, PlannedHeadcount≥1, RequiredPositionId&gt;0,
///     DefaultPlannedWorkUnits &gt; 0 (là % đóng góp của việc vào nhóm cha);
///   - gate (Milestone): không con, sản lượng = 0, PlannedWorkUnits = 0 (không tính vào %);
///   - TRỌNG SỐ THEO NHÓM: mỗi nhóm anh-em (và tập node top-level), bỏ Gate, phải cộng đúng 100
///     ⇒ khi mọi việc xong, tiến độ nhóm/đơn = 100% (ProgressService chuẩn hoá theo tổng trọng số con);
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

        // Kiểm tra tổng trọng số SAU khi build (để lỗi cấu trúc từng node nổi lên trước lỗi tổng nhóm).
        ValidateGroupWeightsSumTo100(roots, "Cấp gốc");
        return flat;
    }

    // Trọng số = % đóng góp: mọi node non-gate trong 1 nhóm anh-em phải cộng đúng 100 ⇒ khi mọi việc xong,
    // tiến độ nhóm = 100% (ProgressService chuẩn hoá theo tổng trọng số con). Gate không tính vào tổng.
    private static void ValidateGroupWeightsSumTo100(IReadOnlyList<ProcessStepInput> siblings, string groupLabel)
    {
        var sum = 0;
        foreach (var s in siblings)
        {
            if (s.DefaultProgressType == ProgressType.Milestone) continue; // gate: không tham gia %
            sum += s.DefaultPlannedWorkUnits;
        }
        if (sum != 100)
            throw new DomainValidationException(
                $"{groupLabel}: tổng trọng số các việc (không tính Gate) phải = 100, hiện tại {sum}. " +
                "Trọng số = % đóng góp của mỗi việc vào nhóm.");
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
            // Gate không tham gia % ⇒ khoá WorkUnits về 0 để bất biến rollup rõ ràng
            // (không phụ thuộc input — user chỉ nhập gate bắt buộc / không bắt buộc).
            node.DefaultIsRequiredGate = input.DefaultIsRequiredGate;
            node.DefaultPlannedQuantity = 0;
            node.DefaultPlannedWorkUnits = 0;
            node.ScalesWithQuantity = false;
        }
        else if (hasChildren)
        {
            // Container: WorkUnits là % đóng góp của nhóm vào nhóm cha — phải > 0 (và cả nhóm cộng đúng 100).
            if (input.DefaultPlannedWorkUnits <= 0)
                throw new DomainValidationException(
                    $"Nhóm '{input.Name}' phải có trọng số (WorkUnits) > 0 (% đóng góp vào nhóm cha).");

            node.DefaultPlannedQuantity = 0;
            node.PlannedHeadcount = 0;
            node.RequiredPositionId = null;
            node.ScalesWithQuantity = false;
        }
        else
        {
            // Work-leaf Quantity: bắt buộc sản lượng + headcount + chức danh + WorkUnits > 0.
            if (input.DefaultPlannedQuantity <= 0)
                throw new DomainValidationException($"Vị trí lá '{input.Name}' phải có sản lượng > 0.");
            if (input.PlannedHeadcount < 1)
                throw new DomainValidationException($"Vị trí lá '{input.Name}' phải có số người ≥ 1.");
            if (input.RequiredPositionId is not > 0)
                throw new DomainValidationException($"Vị trí lá '{input.Name}' phải chỉ định chức danh (RequiredPositionId).");
            if (input.DefaultPlannedWorkUnits <= 0)
                throw new DomainValidationException(
                    $"Vị trí lá '{input.Name}' phải có trọng số (WorkUnits) > 0 (% đóng góp vào nhóm cha).");

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

            ValidateGroupWeightsSumTo100(input.Children!, $"Nhóm '{node.Name}'");
        }

        return node;
    }
}
