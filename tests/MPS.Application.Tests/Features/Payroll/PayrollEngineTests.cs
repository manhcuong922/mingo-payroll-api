using FluentAssertions;
using MPS.Application.Common.Interfaces.Payroll;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Common.Services;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

public sealed class PayrollEngineTests
{
    // ===== Recompute giữ override + tính lại downstream =====
    [Fact]
    public void Recompute_AfterOverride_KeepsPinnedCellAndUpdatesDownstream()
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var catalog = new PayrollFieldCatalog();
        var plan = PayrollColumnPlan.Build(DefaultPayrollFormulaSet.BuildColumns(), graph);

        var inputs = new PayrollEmployeeInputs
        {
            EmployeeInformationId = 1,
            Numbers =
            {
                [PayrollFieldKeys.ProductivityCoefficient] = 1.0m,
                [PayrollFieldKeys.CongChuan] = 23m,
                [PayrollFieldKeys.CongChinhThuc] = 23m,
                [PayrollFieldKeys.ContractMucLuong] = 20_000_000m,
            },
            Texts = { [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive },
        };
        var ctx = new PayrollCalcContext
        {
            Fields = inputs.ToFields(catalog),
            Params = new Dictionary<string, ResolvedValue>
            {
                [PayrollParameterKeys.MealPricePerWorkday] = new(40_000m, "Đơn giá cơm/công"),
            },
            FunctionContext = new PayrollFunctionContext(),
        };
        var row = new PayrollRow();
        calculator.ComputeAll(plan, ctx, row);

        var mucLuong = row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.MucLuong);
        mucLuong.IsOverridden = true;
        mucLuong.OverrideValue = 30_000_000m;

        // Act
        calculator.Recompute(plan, ctx, row, PayrollColumnCodes.MucLuong);

        // Assert — ô ghim giữ nguyên, cột phụ thuộc tính lại theo giá trị ghim.
        mucLuong.IsOverridden.Should().BeTrue();
        mucLuong.OverrideValue.Should().Be(30_000_000m);
        row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.PhuCapLuong).ComputedValue.Should().Be(30_000_000m);
    }

    // ===== Ghi chú thay đổi luật: từ số cũ → số mới =====
    [Fact]
    public void NoteBuilder_WhenCapChanged_EmitsLawChangeNote()
    {
        var builder = new PayrollLawChangeNoteBuilder();
        var current = new List<PayrollComplianceRef>
        {
            new() { VersionType = ComplianceVersionType.SocialInsurance, VersionId = 2, LegalReference = "NĐ 161/2026",
                HeadlineLabel = "Trần đóng BHXH/BHYT", HeadlineValue = 36_000_000m },
        };
        var previous = new List<PayrollComplianceRef>
        {
            new() { VersionType = ComplianceVersionType.SocialInsurance, VersionId = 1, LegalReference = "NĐ 158/2025",
                HeadlineLabel = "Trần đóng BHXH/BHYT", HeadlineValue = 29_800_000m },
        };

        var notes = builder.Build(current, previous);

        notes.Should().Contain(n => n.NoteType == PayrollNoteType.CapThreshold);
        var lawChange = notes.Single(n => n.NoteType == PayrollNoteType.LawChange);
        lawChange.OldValue.Should().Be(29_800_000m);
        lawChange.NewValue.Should().Be(36_000_000m);
        lawChange.Body.Should().Contain("29.800.000").And.Contain("36.000.000");
    }

    // ===== Phát hiện chu trình công thức =====
    [Fact]
    public void DependencyGraph_WhenCyclic_ReportsError()
    {
        var graph = new ColumnDependencyGraph();
        var formulas = new Dictionary<string, FormulaNode?>
        {
            ["A"] = FormulaNode.Col("B"),
            ["B"] = FormulaNode.Col("A"),
        };

        var ok = graph.TryTopologicalOrder(formulas, out _, out var error);

        ok.Should().BeFalse();
        error.Should().Contain("chu trình");
    }

    // ===== Thuế lũy tiến khớp biểu 2026 =====
    [Fact]
    public void ProgressiveTax_ComputesPerBracket()
    {
        var fn = new ProgressiveTaxFunction();
        var ctx = new PayrollFunctionContext
        {
            PitBrackets = new List<PitBracketSnapshot>
            {
                new(1, 0m, 10_000_000m, 0.05m),
                new(2, 10_000_000m, 30_000_000m, 0.10m),
            },
        };

        // TNTT 16,270,000 → 10tr×5% + 6,27tr×10% = 500,000 + 627,000 = 1,127,000
        var result = fn.Invoke(new[] { 16_270_000m }, ctx);
        result.Value.Should().Be(1_127_000m);
    }
}
