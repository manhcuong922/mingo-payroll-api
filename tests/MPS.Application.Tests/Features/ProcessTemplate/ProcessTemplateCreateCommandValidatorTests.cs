using FluentAssertions;
using Moq;
using MPS.Application.Features.ProcessTemplate.Commands;
using MPS.Application.Features.ProcessTemplate.Validators;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.ProcessTemplate;

// Bug đã fix: validator từng bắt DefaultPlannedQuantity>0 và DefaultPlannedWorkUnits>0 cho MỌI step,
// không có nhánh riêng cho step Milestone/Gate — nên không thể tạo step Gate hợp lệ (plan.md mục 5.3/8.4
// yêu cầu Gate step phải có cả 2 giá trị = 0).
public sealed class ProcessTemplateCreateCommandValidatorTests
{
    private readonly Mock<IProcessTemplateRepository> _repository = new();

    private MPS.Application.Features.ProcessTemplate.Validators.ProcessTemplateCreateCommandValidator CreateSut()
    {
        _repository.Setup(r => r.CodeExistsAsync(It.IsAny<string>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        return new ProcessTemplateCreateCommandValidator(_repository.Object);
    }

    private static ProcessTemplateCreateCommand Command(params ProcessStepInput[] steps) =>
        new("KHUNG-A4", "Gia công khung tranh A4", null, null, steps);

    [Fact]
    public async Task Validate_Passes_ForValidQuantityStep()
    {
        var cmd = Command(new ProcessStepInput(
            "Cắt khung", null, 1, ProgressType.Quantity,
            DefaultPlannedQuantity: 500, DefaultPlannedWorkUnits: 500,
            DefaultIsRequiredGate: false, Priority: 2));

        var result = await CreateSut().ValidateAsync(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_Fails_WhenQuantityStepHasZeroPlannedQuantity()
    {
        var cmd = Command(new ProcessStepInput(
            "Cắt khung", null, 1, ProgressType.Quantity,
            DefaultPlannedQuantity: 0, DefaultPlannedWorkUnits: 500,
            DefaultIsRequiredGate: false, Priority: 2));

        var result = await CreateSut().ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Fails_WhenQuantityStepHasZeroPlannedWorkUnits()
    {
        var cmd = Command(new ProcessStepInput(
            "Cắt khung", null, 1, ProgressType.Quantity,
            DefaultPlannedQuantity: 500, DefaultPlannedWorkUnits: 0,
            DefaultIsRequiredGate: false, Priority: 2));

        var result = await CreateSut().ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Passes_ForValidGateStep()
    {
        var cmd = Command(new ProcessStepInput(
            "Nghiệm thu khách hàng", null, 1, ProgressType.Milestone,
            DefaultPlannedQuantity: 0, DefaultPlannedWorkUnits: 0,
            DefaultIsRequiredGate: true, Priority: 2));

        var result = await CreateSut().ValidateAsync(cmd);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_Fails_WhenGateStepHasNonZeroPlannedQuantity()
    {
        var cmd = Command(new ProcessStepInput(
            "Nghiệm thu khách hàng", null, 1, ProgressType.Milestone,
            DefaultPlannedQuantity: 10, DefaultPlannedWorkUnits: 0,
            DefaultIsRequiredGate: true, Priority: 2));

        var result = await CreateSut().ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_Fails_WhenGateStepHasNonZeroPlannedWorkUnits()
    {
        var cmd = Command(new ProcessStepInput(
            "Nghiệm thu khách hàng", null, 1, ProgressType.Milestone,
            DefaultPlannedQuantity: 0, DefaultPlannedWorkUnits: 10,
            DefaultIsRequiredGate: true, Priority: 2));

        var result = await CreateSut().ValidateAsync(cmd);

        result.IsValid.Should().BeFalse();
    }
}
