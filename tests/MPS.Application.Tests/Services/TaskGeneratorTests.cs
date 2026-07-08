using FluentAssertions;
using MPS.Application.Services;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using Xunit;

namespace MPS.Application.Tests.Services;

// Bug đã fix: TaskGenerator từng đặt IsBatchParent=true cho MỌI step, kể cả step Milestone/Gate —
// khiến "gate" sinh ra từ template thực chất là 1 công đoạn rỗng (IsBatchParent=true), sai model
// plan.md mục 5.3 (step Gate phải sinh IsBatchParent=false, PlannedQuantity=0, PlannedWorkUnits=0).
public sealed class TaskGeneratorTests
{
    private static Project Project() => new() { Id = 1, DepartmentId = 3 };

    private static ProcessTemplate Template(params ProcessStep[] steps)
    {
        var t = new ProcessTemplate { Id = 9, DefaultDepartmentId = 3 };
        t.Steps.AddRange(steps);
        return t;
    }

    [Fact]
    public void BuildStages_QuantityStep_ProducesBatchParentStage()
    {
        var template = Template(new ProcessStep
        {
            Id = 1, Name = "Cắt khung", StepOrder = 1,
            DefaultProgressType = ProgressType.Quantity,
            DefaultPlannedQuantity = 500, DefaultPlannedWorkUnits = 400,
        });

        var stages = new TaskGenerator().BuildStages(Project(), template, createdByUserId: 7, overrides: null);

        stages.Should().HaveCount(1);
        var stage = stages[0];
        stage.IsBatchParent.Should().BeTrue();
        stage.ProgressType.Should().Be(ProgressType.Quantity);
        stage.PlannedQuantity.Should().Be(500);
        stage.PlannedWorkUnits.Should().Be(400);
    }

    [Fact]
    public void BuildStages_GateStep_ProducesNonBatchParentGateWithZeroQuantityAndWorkUnits()
    {
        var template = Template(new ProcessStep
        {
            Id = 2, Name = "Nghiệm thu khách hàng", StepOrder = 1,
            DefaultProgressType = ProgressType.Milestone,
            DefaultIsRequiredGate = true,
            // Vẫn set default quantity/work units khác 0 để chứng minh generator luôn ép 0 cho gate,
            // bất kể dữ liệu step (phòng trường hợp step bị cấu hình sai qua đường khác).
            DefaultPlannedQuantity = 999, DefaultPlannedWorkUnits = 999,
        });

        var stages = new TaskGenerator().BuildStages(Project(), template, createdByUserId: 7, overrides: null);

        stages.Should().HaveCount(1);
        var gate = stages[0];
        gate.IsBatchParent.Should().BeFalse();
        gate.ProgressType.Should().Be(ProgressType.Milestone);
        gate.PlannedQuantity.Should().Be(0);
        gate.PlannedWorkUnits.Should().Be(0);
        gate.IsRequiredGate.Should().BeTrue();
        gate.AssignedPmUserId.Should().BeNull();
    }

    [Fact]
    public void BuildStages_GateStep_NotRequired_KeepsIsRequiredGateFalse()
    {
        var template = Template(new ProcessStep
        {
            Id = 3, Name = "Chụp ảnh lưu hồ sơ (tuỳ chọn)", StepOrder = 1,
            DefaultProgressType = ProgressType.Milestone,
            DefaultIsRequiredGate = false,
        });

        var stages = new TaskGenerator().BuildStages(Project(), template, createdByUserId: 7, overrides: null);

        stages[0].IsRequiredGate.Should().BeFalse();
    }

    [Fact]
    public void BuildStages_MixedSteps_PreservesOrderAndEachKind()
    {
        var template = Template(
            new ProcessStep
            {
                Id = 1, Name = "Cắt khung", StepOrder = 1,
                DefaultProgressType = ProgressType.Quantity,
                DefaultPlannedQuantity = 500, DefaultPlannedWorkUnits = 500,
            },
            new ProcessStep
            {
                Id = 2, Name = "Nghiệm thu", StepOrder = 2,
                DefaultProgressType = ProgressType.Milestone, DefaultIsRequiredGate = true,
            });

        var stages = new TaskGenerator().BuildStages(Project(), template, createdByUserId: 7, overrides: null);

        stages.Should().HaveCount(2);
        stages[0].IsBatchParent.Should().BeTrue();
        stages[1].IsBatchParent.Should().BeFalse();
    }
}
