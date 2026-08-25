using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// ⭐ A4 (phần Thưởng dự án) — cột <c>THUONGDUAN</c> từng khai <c>Lookup</c> vào một field mà
/// collector luôn đổ 0, nên nó chết cứng ở 0 với MỌI dữ liệu: kế toán không sửa được (không phải
/// Input), hệ thống cũng không tra được (không có bảng phân bổ thưởng theo dự án/NLĐ).
///
/// <para>Quyết định PO 19/08/2026: mở thành <b>Input</b> cho kế toán nhập tay như Thưởng năm /
/// Thưởng khác ngay cạnh. Dựng nguồn dữ liệu thật là một module riêng, chưa làm.</para>
/// </summary>
public sealed class ProjectBonusColumnTests
{
    [Fact(DisplayName = "A4 · Cột Thưởng dự án là Input và kế toán sửa được")]
    public void ProjectBonus_IsEditableInput()
    {
        var col = DefaultPayrollFormulaSet.BuildColumns()
            .Single(c => c.ColumnCode == PayrollColumnCodes.ThuongDuAn);

        col.ValueSource.Should().Be(ColumnValueSource.Input);
        col.IsEditable.Should().BeTrue();
        col.LookupKey.Should().BeNull("tra vào field hardcode 0 chính là thứ khoá cột ở 0 suốt thời gian qua");
    }

    [Fact(DisplayName = "A4 · Số kế toán nhập chảy vào Tổng thu nhập")]
    public void ManualEntry_FlowsIntoTotalIncome()
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var plan = PayrollColumnPlan.Build(DefaultPayrollFormulaSet.BuildColumns(), graph);
        var ctx = new PayrollCalcContext
        {
            Fields = new PayrollEmployeeInputs
            {
                EmployeeInformationId = 1,
                Numbers =
                {
                    [PayrollFieldKeys.ContractMucLuong] = 10_000_000m,
                    [PayrollFieldKeys.CongChuan] = 26m,
                    [PayrollFieldKeys.CongTinhCom] = 26m,
                    [PayrollFieldKeys.CongChinhThuc] = 26m,
                },
                Texts =
                {
                    [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.NoInsurance,
                    [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.CamKet,
                },
            }.ToFields(new PayrollFieldCatalog()),
            FunctionContext = new PayrollFunctionContext(),
        };

        var row = new PayrollRow { RowType = PayrollRowType.Employee, EmployeeInformationId = 1 };
        calculator.ComputeAll(plan, ctx, row);
        var before = Cell(row, PayrollColumnCodes.TongThuNhap);

        // Kế toán nhập tay rồi hệ thống tính lại các cột phụ thuộc — đúng đường của endpoint sửa ô.
        var bonusCell = row.Cells.Single(c => c.ColumnCode == PayrollColumnCodes.ThuongDuAn);
        bonusCell.IsOverridden = true;
        bonusCell.OverrideValue = 5_000_000m;
        calculator.Recompute(plan, ctx, row, PayrollColumnCodes.ThuongDuAn);

        Cell(row, PayrollColumnCodes.TongThuNhap).Should().Be(before + 5_000_000m);
    }

    private static decimal Cell(PayrollRow row, string code)
        => row.Cells.Single(c => c.ColumnCode == code).ComputedValue ?? 0m;
}
