using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Excel;
using MPS.Application.Common.Payroll.Formula;
using MPS.Application.Features.Payroll.Common;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

/// <summary>
/// Dựng workbook từ snapshot của kỳ: bố cục clone file mẫu, hằng số vào hàng 9/10 + named range,
/// hệ số năng suất thành cột phụ trợ, và công thức Excel của từng ô khớp bảng công thức.
/// Dữ liệu dùng chính dòng F5-0001 của file lương mẫu 08/2023 (xem PayrollGoldenReconciliationTests).
/// </summary>
public sealed class PayrollExcelBookBuilderTests
{
    private static List<PitBracketSnapshot> Pit2023() =>
    [
        new(1, 0m, 5_000_000m, 0.05m),
        new(2, 5_000_000m, 10_000_000m, 0.10m),
        new(3, 10_000_000m, 18_000_000m, 0.15m),
        new(4, 18_000_000m, 32_000_000m, 0.20m),
        new(5, 32_000_000m, 52_000_000m, 0.25m),
        new(6, 52_000_000m, 80_000_000m, 0.30m),
        new(7, 80_000_000m, null, 0.35m),
    ];

    private static PayrollEmployeeInputs Inputs(
        int id, decimal mucLuong, decimal heSo,
        decimal tangCa = 0m, decimal chenhLechLtg = 0m, decimal doanPhi = 0m) => new()
    {
        EmployeeInformationId = id,
        Numbers =
        {
            [PayrollFieldKeys.ContractMucLuong] = mucLuong,
            [PayrollFieldKeys.ProductivityCoefficient] = heSo,
            [PayrollFieldKeys.CongChuan] = 23m,
            [PayrollFieldKeys.CongChinhThuc] = 23m,
            [PayrollFieldKeys.CongTinhCom] = 24m,
            [PayrollFieldKeys.ContractAllowanceDienThoai] = 500_000m,
            [PayrollFieldKeys.ContractAllowanceXangXe] = 500_000m,
            [PayrollFieldKeys.DependentsCount] = 2m,
            [PayrollFieldKeys.OtTangCa] = tangCa,
            [PayrollFieldKeys.OtChenhLech] = chenhLechLtg,
            [PayrollFieldKeys.UnionFee] = doanPhi,
            [PayrollFieldKeys.BhxhDnRate] = 0.175m,
            [PayrollFieldKeys.BhxhNldRate] = 0.08m,
            [PayrollFieldKeys.BhytDnRate] = 0.03m,
            [PayrollFieldKeys.BhytNldRate] = 0.015m,
            [PayrollFieldKeys.BhtnDnRate] = 0.01m,
            [PayrollFieldKeys.BhtnNldRate] = 0.01m,
            [PayrollFieldKeys.KpcdRate] = 0.02m,
            [PayrollFieldKeys.BhxhBhytCap] = 29_800_000m,
            [PayrollFieldKeys.BhtnCap] = 93_600_000m,
            [PayrollFieldKeys.PersonalDeduction] = 11_000_000m,
            [PayrollFieldKeys.DependentDeduction] = 4_400_000m,
            [PayrollFieldKeys.MealExemptThreshold] = 730_000m,
        },
        Texts =
        {
            [PayrollFieldKeys.EmployeeCode] = $"F5-{id:0000}",
            [PayrollFieldKeys.EmployeeFullName] = $"Nhân viên {id}",
            [PayrollFieldKeys.ContractInsuranceFlag] = InsuranceFlags.Full,
            [PayrollFieldKeys.ContractTaxFlag] = TaxFlags.Progressive,
        },
    };

    private static PayrollPeriod BuildPeriod(params PayrollEmployeeInputs[] employees)
    {
        var graph = new ColumnDependencyGraph();
        var calculator = new PayrollCalculator(new FormulaEvaluator(new FormulaFunctionRegistry()), graph);
        var catalog = new PayrollFieldCatalog();
        var columns = DefaultPayrollFormulaSet.BuildColumns();
        var plan = PayrollColumnPlan.Build(columns, graph);

        var period = new PayrollPeriod
        {
            Id = 1,
            PeriodYear = 2023,
            PeriodMonth = 8,
            PayDate = new DateOnly(2023, 8, 31),
            StandardWorkingDays = 23m,
            Status = PayrollPeriodStatus.Draft,
            FormulaSnapshotJson = PayrollColumnSnapshotMapper.Serialize(columns),
            ParamsSnapshotJson = PayrollJson.Serialize(new[]
            {
                new { Key = PayrollParameterKeys.MealPricePerWorkday, Name = "Đơn giá cơm/công", Value = 40_000m },
            }),
            ProductivitySnapshotJson = "[]",
            PitBracketsSnapshotJson = PayrollJson.Serialize(Pit2023()),
        };

        var rows = new List<PayrollRow>();
        var order = 0;
        foreach (var e in employees)
        {
            var ctx = new PayrollCalcContext
            {
                Fields = e.ToFields(catalog),
                Params = new Dictionary<string, ResolvedValue>
                {
                    [PayrollParameterKeys.MealPricePerWorkday] = new(40_000m, "Đơn giá cơm/công"),
                },
                FunctionContext = new PayrollFunctionContext { PitBrackets = Pit2023() },
            };
            var row = new PayrollRow
            {
                Id = 100 + order,
                RowType = PayrollRowType.Employee,
                EmployeeInformationId = e.EmployeeInformationId,
                DisplayOrder = order++,
                InputSnapshotJson = e.ToSnapshotJson(),
            };
            calculator.ComputeAll(plan, ctx, row);
            rows.Add(row);
            period.Rows.Add(row);
        }

        var total = new PayrollRow { Id = 999, RowType = PayrollRowType.Total, DisplayOrder = order };
        calculator.ComputeTotals(plan, rows, total);
        period.Rows.Add(total);
        return period;
    }

    private static PayrollExcelBook Build(PayrollPeriod period) =>
        new PayrollExcelBookBuilder(new PayrollFieldCatalog()).Build(period, "Công ty TNHH Mingo");

    private static PayrollExcelColumn Column(PayrollExcelBook book, string code) =>
        book.Columns.Single(c => c.ColumnCode == code);

    private static PayrollExcelCell Cell(PayrollExcelBook book, int rowIndex, string code) =>
        book.Rows[rowIndex].Cells[book.Columns.ToList().FindIndex(c => c.ColumnCode == code)];

    [Fact]
    public void Sheet_starts_with_STT_then_employee_code_at_column_B_like_the_sample()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m)));

        book.Columns[0].ColumnCode.Should().Be("STT");
        book.Columns[0].Letter.Should().Be("A");
        Column(book, PayrollColumnCodes.MaNv).Letter.Should().Be("B");
    }

    [Fact]
    public void Productivity_coefficient_becomes_a_helper_column_right_before_the_column_using_it()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m)));

        var heSo = Column(book, PayrollFieldKeys.ProductivityCoefficient);
        var phuCap = Column(book, PayrollColumnCodes.PhuCapLuong);

        heSo.FieldKey.Should().Be(PayrollFieldKeys.ProductivityCoefficient);
        heSo.Index.Should().Be(phuCap.Index - 1);
        heSo.GroupName.Should().Be(phuCap.GroupName);
    }

    [Fact]
    public void Productivity_coefficient_stays_a_column_even_when_every_employee_shares_the_same_value()
    {
        // Kỳ 1 nhân viên (hoặc mọi NV cùng hệ số) không được biến dữ liệu-theo-NV thành hằng số.
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m), Inputs(2, 7_700_000m, 2.2m)));

        book.Constants.Should().NotContain(k => k.Key == PayrollFieldKeys.ProductivityCoefficient);
        Column(book, PayrollFieldKeys.ProductivityCoefficient).Should().NotBeNull();
    }

    [Fact]
    public void Period_constants_land_on_rows_9_and_10_with_named_ranges()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m)));

        var cap = book.Constants.Single(k => k.Key == PayrollFieldKeys.BhxhBhytCap);
        cap.Name.Should().Be("TRAN_BHXH_BHYT");
        cap.Value.Should().Be(29_800_000m);
        cap.SheetName.Should().Be(PayrollExcelLayout.SheetSalary);
        cap.CellRef.Should().EndWith("10");           // trần → hàng 10
        cap.IsPercent.Should().BeFalse();

        var rate = book.Constants.Single(k => k.Key == PayrollFieldKeys.BhxhNldRate);
        rate.Name.Should().Be("TYLE_BHXH_NLD");
        rate.CellRef.Should().EndWith("9");           // tỷ lệ → hàng 9
        rate.IsPercent.Should().BeTrue();

        // Giá cơm nằm hàng 9 nhưng là TIỀN, không phải phần trăm.
        var giaCom = book.Constants.Single(k => k.Key == PayrollParameterKeys.MealPricePerWorkday);
        giaCom.Name.Should().Be("GIA_COM");
        giaCom.Value.Should().Be(40_000m);
        giaCom.CellRef.Should().EndWith("9");
        giaCom.IsPercent.Should().BeFalse();
    }

    [Fact]
    public void Named_range_targets_are_absolute_so_Excel_resolves_them_from_any_row()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m)));

        book.Constants.Single(k => k.Key == PayrollParameterKeys.MealPricePerWorkday)
            .AbsoluteRef.Should().MatchRegex(@"^'4\. LUONG'!\$[A-Z]+\$9$");
    }

    [Fact]
    public void Meal_cell_formula_reads_the_named_range_and_the_insurance_flag()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m)));

        var com = Column(book, PayrollColumnCodes.Com);
        var congTinhCom = Column(book, PayrollColumnCodes.CongTinhCom);
        var flag = Column(book, PayrollColumnCodes.FlagBh);

        // Cột tiền không khai báo làm tròn vẫn được engine làm tròn về đồng nguyên → phải có ROUND(,0).
        Cell(book, 0, PayrollColumnCodes.Com).Formula.Should()
            .Be($"ROUND(IF(EXACT({flag.Letter}13,\"xx\"),0,({congTinhCom.Letter}13*GIA_COM)),0)");
    }

    [Fact]
    public void Take_home_formula_is_rounded_to_thousands_like_the_spec()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m)));

        var thucNhan = Cell(book, 0, PayrollColumnCodes.ThucNhan);

        thucNhan.Formula.Should().StartWith("ROUND(");
        thucNhan.Formula.Should().EndWith(",-3)");
    }

    [Fact]
    public void Cached_values_equal_the_engine_result_so_the_file_opens_showing_the_official_numbers()
    {
        // Đúng bộ input F5-0001 của file lương mẫu 08/2023 → thực nhận 47.049.000 (đặc tả §8).
        var f5_0001 = Inputs(1, 14_641_000m, 2.2m,
            tangCa: 4_074_017.392m, chenhLechLtg: 2_037_008.696m, doanPhi: 292_820m);

        var book = Build(BuildPeriod(f5_0001));

        Cell(book, 0, PayrollColumnCodes.PhuCapLuong).Number.Should().Be(32_210_200m);
        Cell(book, 0, PayrollColumnCodes.ThuNhap).Number.Should().Be(48_811_200m);
        Cell(book, 0, PayrollColumnCodes.ThucNhan).Number.Should().Be(47_049_000m);
    }

    [Fact]
    public void Overridden_cell_drops_its_formula_and_explains_itself_in_a_comment()
    {
        var period = BuildPeriod(Inputs(1, 14_641_000m, 2.2m));
        var cell = period.Rows.First().Cells.Single(c => c.ColumnCode == PayrollColumnCodes.ThueTncn);
        cell.IsOverridden = true;
        cell.OverrideValue = 1_000_000m;

        var book = Build(period);
        var exported = Cell(book, 0, PayrollColumnCodes.ThueTncn);

        exported.Number.Should().Be(1_000_000m);
        exported.Formula.Should().BeNull();
        exported.IsOverridden.Should().BeTrue();
        exported.Comment.Should().Contain("sửa tay").And.Contain("Giá trị hệ thống tính");
    }

    [Fact]
    public void Total_row_sums_money_columns_over_the_data_range_and_is_labelled()
    {
        var book = Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m), Inputs(2, 7_700_000m, 2.0m)));

        var thucNhan = Column(book, PayrollColumnCodes.ThucNhan);
        var totalIndex = book.Columns.ToList().FindIndex(c => c.ColumnCode == PayrollColumnCodes.ThucNhan);

        book.TotalCells[totalIndex].Formula.Should().Be($"SUM({thucNhan.Letter}13:{thucNhan.Letter}14)");
        book.TotalCells[1].Text.Should().Be("Cộng");    // cột B như file mẫu
    }

    [Fact]
    public void Law_change_notes_carry_old_new_and_legal_basis_into_the_book()
    {
        var period = BuildPeriod(Inputs(1, 14_641_000m, 2.2m));
        period.Notes.Add(new PayrollNote
        {
            Id = 1,
            NoteType = PayrollNoteType.LawChange,
            ColumnCode = PayrollColumnCodes.GtBanThan,
            Title = "Thay đổi luật: Thuế TNCN",
            Body = "Giảm trừ bản thân: 9.000.000 → 11.000.000 theo NQ 954/2020/UBTVQH14.",
            OldValue = 9_000_000m,
            NewValue = 11_000_000m,
            LegalReference = "NQ 954/2020/UBTVQH14",
            AutoGenerated = true,
        });

        var book = Build(period);

        var note = book.Notes.Should().ContainSingle(n => n.NoteType == nameof(PayrollNoteType.LawChange)).Subject;
        note.Body.Should().Contain("9.000.000 → 11.000.000");
        note.OldValue.Should().Be(9_000_000m);
        note.NewValue.Should().Be(11_000_000m);
        note.LegalReference.Should().Be("NQ 954/2020/UBTVQH14");
    }

    [Fact]
    public void File_name_carries_the_period()
    {
        Build(BuildPeriod(Inputs(1, 14_641_000m, 2.2m))).FileName.Should().Be("bang-luong-08-2023.xlsx");
    }
}
