using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Commands;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// A6 — Cờ bảo hiểm gõ tay sai chính tả ⇒ hệ thống lặng lẽ tính sai.
///
/// <para><see cref="EditPayrollCellCommandHandler"/> nhận CHUỖI BẤT KỲ cho cột <c>FLAGBH</c>
/// (chỉ <c>Trim()</c> rồi lưu). Công thức bảo hiểm so cờ bằng <c>StringComparison.Ordinal</c>
/// (<c>FormulaEvaluator.Compare</c>), nên <c>"ts"</c> không khớp <c>"TS"</c> ⇒ NV nghỉ thai sản
/// vẫn bị trừ đủ BHXH/BHYT/BHTN. Không lỗi, không cảnh báo.</para>
/// </summary>
public sealed class EditPayrollCellFlagValidationTests
{
    private const int PeriodId = 5;
    private const int RowId = 11;
    private const int UserId = 3;

    private readonly Mock<IPayrollPeriodRepository> _periods = new();
    private readonly Mock<IPayrollCalculator> _calculator = new();
    private readonly Mock<IColumnDependencyGraph> _graph = new();
    private readonly Mock<IPayrollFieldCatalog> _catalog = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public EditPayrollCellFlagValidationTests()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(UserId);
        _catalog.Setup(c => c.GetAll()).Returns(Array.Empty<PayrollFieldDescriptor>());
        _graph.Setup(g => g.TopologicalOrder(It.IsAny<IReadOnlyDictionary<string, FormulaNode?>>()))
            .Returns(Array.Empty<string>());
        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DraftPeriodWithFlagColumn);
    }

    private EditPayrollCellCommandHandler Sut() => new(
        _periods.Object, _calculator.Object, _graph.Object, _catalog.Object, _currentUser.Object);

    private static PayrollPeriod DraftPeriodWithFlagColumn()
    {
        var flagColumn = new SalaryColumnDefinition
        {
            ColumnCode = PayrollColumnCodes.FlagBh,
            Label = "Cờ BH",
            DataType = ColumnDataType.Text,
            ValueSource = ColumnValueSource.Lookup,
            LookupKey = PayrollFieldKeys.ContractInsuranceFlag,
            IsSystem = true,
            IsEditable = true,
        };

        var period = new PayrollPeriod
        {
            Id = PeriodId,
            PeriodYear = 2026,
            PeriodMonth = 7,
            PayDate = new DateOnly(2026, 8, 5),
            StandardWorkingDays = 26m,
            Status = PayrollPeriodStatus.Draft,
            FormulaSnapshotJson = PayrollColumnSnapshotMapper.Serialize(new[] { flagColumn }),
            PitBracketsSnapshotJson = "[]",
        };

        var row = new PayrollRow
        {
            Id = RowId,
            RowType = PayrollRowType.Employee,
            EmployeeInformationId = 42,
            InputSnapshotJson = "{}",
        };
        row.Cells.Add(new PayrollCellValue { ColumnCode = PayrollColumnCodes.FlagBh, TextValue = InsuranceFlags.Full });
        period.Rows.Add(row);
        return period;
    }

    private async Task<Exception?> EditFlagAsync(string flagText)
    {
        try
        {
            await Sut().Handle(
                new EditPayrollCellCommand(PeriodId, RowId, PayrollColumnCodes.FlagBh, null, flagText),
                CancellationToken.None);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    [Theory(DisplayName = "A6 · Cờ BH không hợp lệ phải bị từ chối, không được lưu im lặng")]
    [InlineData("ts")]      // thai sản gõ thường — case-sensitive nên KHÔNG khớp "TS"
    [InlineData("X1")]      // "x1" gõ hoa
    [InlineData("abc")]     // rác
    [InlineData("x4")]      // cờ không tồn tại nhưng "trông giống thật"
    public async Task A6_InvalidInsuranceFlag_MustBeRejected(string flagText)
    {
        var ex = await EditFlagAsync(flagText);

        ex.Should().BeOfType<DomainValidationException>(
            $"'{flagText}' không thuộc tập cờ hợp lệ ({string.Join("/", InsuranceFlags.All)}); " +
            "nhận bừa rồi so sánh Ordinal ⇒ cờ không khớp gì cả và hệ thống trừ đủ bảo hiểm trong im lặng");
    }

    [Theory(DisplayName = "A6 · Đối chứng — cờ hợp lệ vẫn phải lưu được")]
    [InlineData(InsuranceFlags.Maternity)]
    [InlineData(InsuranceFlags.NoInsurance)]
    [InlineData(InsuranceFlags.HealthOnly)]
    [InlineData(InsuranceFlags.NoUnion)]
    public async Task A6_Control_ValidInsuranceFlag_IsAccepted(string flagText)
    {
        var ex = await EditFlagAsync(flagText);

        ex.Should().BeNull("cờ hợp lệ phải được ghim bình thường — validation không được chặn nhầm");
    }

    [Fact(DisplayName = "A6 · Cờ 'ts' (gõ thường) bị chặn và KHÔNG được ghi vào ô")]
    public async Task A6_LowercaseMaternityFlag_IsRejected_AndNothingIsStored()
    {
        PayrollPeriod? saved = null;
        _periods.Setup(p => p.GetFullAsync(PeriodId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => saved = DraftPeriodWithFlagColumn());

        var ex = await EditFlagAsync("ts");

        ex.Should().BeOfType<DomainValidationException>();
        ex!.Message.Should().Contain("TS",
            "thông báo phải liệt kê giá trị hợp lệ để kế toán sửa được ngay, không phải đoán");

        saved!.Rows.Single(r => r.RowType == PayrollRowType.Employee)
            .Cells.Single(c => c.ColumnCode == PayrollColumnCodes.FlagBh)
            .TextValue.Should().Be(InsuranceFlags.Full,
                "ô phải giữ nguyên giá trị cũ — không được lưu nửa vời rồi mới báo lỗi");
    }

    [Fact(DisplayName = "A6 · Gỡ ghim (để trống) vẫn phải chạy được, không bị validation chặn nhầm")]
    public async Task A6_ClearingTheOverride_IsStillAllowed()
    {
        var ex = await EditFlagAsync("");

        ex.Should().BeNull(
            "chuỗi rỗng là thao tác GỠ ghim, đưa ô về giá trị máy suy từ hợp đồng — không phải cờ sai");
    }
}
