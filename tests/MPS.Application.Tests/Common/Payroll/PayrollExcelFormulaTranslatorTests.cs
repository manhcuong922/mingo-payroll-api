using FluentAssertions;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Excel;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Enums.Payroll;
using N = MPS.Application.Common.Payroll.Formula.FormulaNode;

namespace MPS.Application.Tests.Common.Payroll;

/// <summary>
/// Dịch AST → công thức Excel THẬT. Trọng tâm là 4 chỗ engine ≠ Excel — sai 1 chỗ là số trong file
/// lệch số hệ thống: chia 0, so sánh chuỗi phân biệt hoa/thường, hàm nội bộ, làm tròn theo cột.
/// </summary>
public sealed class PayrollExcelFormulaTranslatorTests
{
    private static PayrollExcelRefs Refs(IReadOnlyList<PitBracketSnapshot>? brackets = null) => new(
        ColumnRefs: new Dictionary<string, string>
        {
            [PayrollColumnCodes.MucLuong] = "O13",
            [PayrollColumnCodes.CongTinhCom] = "K13",
            [PayrollColumnCodes.FlagBh] = "I13",
            [PayrollColumnCodes.Com] = "V13",
            [PayrollColumnCodes.Tntt] = "AW13",
            [PayrollColumnCodes.CongChuan] = "J13",
        },
        FieldRefs: new Dictionary<string, string>
        {
            [PayrollFieldKeys.BhxhNldRate] = "TYLE_BHXH_NLD",
            [PayrollFieldKeys.BhxhBhytCap] = "TRAN_BHXH_BHYT",
            [PayrollFieldKeys.ProductivityCoefficient] = "P13",
            [PayrollFieldKeys.ContractInsuranceFlag] = "I13",
        },
        TextColumns: new HashSet<string> { PayrollColumnCodes.FlagBh },
        TextFields: new HashSet<string> { PayrollFieldKeys.ContractInsuranceFlag },
        PitBrackets: brackets ?? Array.Empty<PitBracketSnapshot>());

    [Fact]
    public void Translate_maps_column_and_field_to_cells_and_named_ranges()
    {
        // field hằng-số-của-kỳ → named range; cột → ô cùng dòng.
        var ast = N.Mul(N.Col(PayrollColumnCodes.MucLuong), N.Field(PayrollFieldKeys.BhxhNldRate));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("(O13*TYLE_BHXH_NLD)");
    }

    [Fact]
    public void Translate_uses_IFERROR_for_division_because_engine_returns_zero_when_dividing_by_zero()
    {
        var ast = N.Div(N.Col(PayrollColumnCodes.MucLuong), N.Col(PayrollColumnCodes.CongChuan));

        // Engine: chia 0 → 0. Excel thuần: #DIV/0!.
        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("IFERROR(O13/J13,0)");
    }

    [Fact]
    public void Translate_uses_EXACT_for_text_compare_because_Excel_equals_ignores_case()
    {
        var ast = N.Cmp("=", N.Col(PayrollColumnCodes.FlagBh), N.Literal(InsuranceFlags.NoMealFlatTax));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("EXACT(I13,\"xx\")");
    }

    [Fact]
    public void Translate_uses_NOT_EXACT_for_text_inequality()
    {
        var ast = N.Cmp("<>", N.Col(PayrollColumnCodes.FlagBh), N.Literal(InsuranceFlags.Full));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("NOT(EXACT(I13,\"x3\"))");
    }

    [Fact]
    public void Translate_keeps_plain_operator_for_numeric_compare()
    {
        var ast = N.Cmp(">", N.Col(PayrollColumnCodes.MucLuong), N.Const(29_800_000m));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("(O13>29800000)");
    }

    [Fact]
    public void Translate_expands_insurance_with_cap_into_native_Excel()
    {
        var ast = N.Func(PayrollFunctions.InsuranceWithCap,
            N.Col(PayrollColumnCodes.MucLuong),
            N.Field(PayrollFieldKeys.BhxhNldRate),
            N.Field(PayrollFieldKeys.BhxhBhytCap));

        // cap ≤ 0 nghĩa là "không trần" trong engine (KPCĐ truyền cap = 0) → phải giữ nhánh IF.
        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should()
            .Be("(IF(TRAN_BHXH_BHYT>0,MIN(O13,TRAN_BHXH_BHYT),O13)*TYLE_BHXH_NLD)");
    }

    [Fact]
    public void Translate_expands_progressive_tax_from_period_brackets_not_hardcoded_rates()
    {
        var brackets = new List<PitBracketSnapshot>
        {
            new(1, 0m, 5_000_000m, 0.05m),
            new(2, 5_000_000m, 10_000_000m, 0.10m),
            new(3, 10_000_000m, null, 0.15m),
        };
        var ast = N.Func(PayrollFunctions.ProgressiveTax, N.Col(PayrollColumnCodes.Tntt));

        var formula = PayrollExcelFormulaTranslator.Translate(ast, Refs(brackets));

        // Bậc 1 rút gọn (luỹ kế 0, cận dưới 0); bậc sau cộng thuế luỹ kế tới cận dưới.
        formula.Should().Be(
            "IF(AW13<=0,0," +
            "IF(AW13<=5000000,AW13*0.05," +
            "IF(AW13<=10000000,(250000+(AW13-5000000)*0.1)," +
            "(750000+(AW13-10000000)*0.15))))");
    }

    [Fact]
    public void Progressive_tax_brackets_reproduce_the_quick_deduction_table_of_the_spec()
    {
        // Đặc tả §5: bậc 2 trừ nhanh 250.000 ⇔ luỹ kế 5tr×5% = 250.000. Bậc 3 trừ nhanh 750.000
        // ⇔ 250.000 + 5tr×10% = 750.000. Hai cách viết cho cùng kết quả.
        var brackets = new List<PitBracketSnapshot>
        {
            new(1, 0m, 5_000_000m, 0.05m),
            new(2, 5_000_000m, 10_000_000m, 0.10m),
            new(3, 10_000_000m, 18_000_000m, 0.15m),
            new(4, 18_000_000m, null, 0.20m),
        };
        var ast = N.Func(PayrollFunctions.ProgressiveTax, N.Col(PayrollColumnCodes.Tntt));

        var formula = PayrollExcelFormulaTranslator.Translate(ast, Refs(brackets));

        formula.Should().Contain("(250000+(AW13-5000000)*0.1)");
        formula.Should().Contain("(750000+(AW13-10000000)*0.15)");
        formula.Should().Contain("(1950000+(AW13-18000000)*0.2)");
    }

    [Fact]
    public void Translate_expands_income_proration_with_the_zero_standard_days_guard()
    {
        var ast = N.Func(PayrollFunctions.IncomeProration,
            N.Const(100m), N.Const(1m), N.Const(2m), N.Const(3m),
            N.Col(PayrollColumnCodes.CongChuan), N.Const(5m), N.Const(6m), N.Const(7m), N.Const(8m));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should()
            .Be("IF(J13=0,8,(((100-2-3)/J13*5)+((100-1-3)/J13*6)+((3/J13)*7)+8))");
    }

    [Theory]
    [InlineData(RoundingMode.None, ColumnDataType.Number, "O13")]
    [InlineData(RoundingMode.ToThreeDecimals, ColumnDataType.Money, "ROUND(O13,3)")]
    [InlineData(RoundingMode.ToThousand, ColumnDataType.Money, "ROUND(O13,-3)")]
    public void Wrap_applies_the_column_rounding_that_lives_outside_the_ast(
        RoundingMode mode, ColumnDataType type, string expected)
    {
        PayrollExcelFormulaTranslator.Wrap("O13", mode, type).Should().Be(expected);
    }

    [Fact]
    public void Wrap_rounds_money_columns_to_whole_dong_even_without_an_explicit_rounding_mode()
    {
        // PayrollRounding.ApplyForColumn làm tròn MỌI cột tiền về đồng nguyên khi mode = None.
        // Quên bước này thì Excel (bật fullCalcOnLoad) tính ra số lẻ ≠ số hệ thống ngay khi mở file.
        PayrollExcelFormulaTranslator.Wrap("(O13*P13)", RoundingMode.None, ColumnDataType.Money)
            .Should().Be("ROUND((O13*P13),0)");
    }

    [Fact]
    public void Wrap_matches_the_engine_rounding_for_every_mode_and_data_type_combination()
    {
        // Probe có phần lẻ ở mọi bậc nên bất kỳ chế độ làm tròn nào cũng đổi giá trị.
        const decimal probe = 1234.56789m;

        foreach (var mode in Enum.GetValues<RoundingMode>())
        foreach (var type in Enum.GetValues<ColumnDataType>())
        {
            var engineRounds = PayrollRounding.ApplyForColumn(probe, mode, type) != probe;

            PayrollExcelFormulaTranslator.Wrap("X", mode, type).StartsWith("ROUND(")
                .Should().Be(engineRounds, $"engine {(engineRounds ? "" : "KHÔNG ")}làm tròn cột {type}/{mode}");
        }
    }

    [Fact]
    public void Translate_throws_when_a_referenced_column_has_no_cell_instead_of_emitting_a_wrong_formula()
    {
        var ast = N.Col("KHONG_TON_TAI");

        var act = () => PayrollExcelFormulaTranslator.Translate(ast, Refs());

        act.Should().Throw<InvalidOperationException>().WithMessage("*KHONG_TON_TAI*");
    }

    [Fact]
    public void Translate_escapes_double_quotes_in_text_literals()
    {
        var ast = N.Cmp("=", N.Col(PayrollColumnCodes.FlagBh), N.Literal("a\"b"));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("EXACT(I13,\"a\"\"b\")");
    }

    [Fact]
    public void Text_node_in_a_numeric_slot_becomes_zero_because_that_is_what_the_engine_does()
    {
        // FormulaEvaluator.TextNode luôn trả Value = 0 → Excel phải là 0, không phải "xx" (#VALUE!).
        var ast = N.Add(N.Col(PayrollColumnCodes.MucLuong), N.Col(PayrollColumnCodes.FlagBh));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("(O13+0)");
    }

    [Fact]
    public void If_condition_that_is_not_a_comparison_reproduces_the_engine_truthiness_rule()
    {
        // Engine IsTruthy: chuỗi → không rỗng; số → khác 0. Excel IF(text,…) thì ra #VALUE!.
        var onText = N.If(N.Col(PayrollColumnCodes.FlagBh), N.Const(1m), N.Const(2m));
        var onNumber = N.If(N.Col(PayrollColumnCodes.MucLuong), N.Const(1m), N.Const(2m));

        PayrollExcelFormulaTranslator.Translate(onText, Refs()).Should().Be("IF((LEN(I13)>0),1,2)");
        PayrollExcelFormulaTranslator.Translate(onNumber, Refs()).Should().Be("IF((O13<>0),1,2)");
    }

    [Fact]
    public void Comparing_text_against_a_number_literal_uses_the_engine_vietnamese_formatting()
    {
        // Engine so chuỗi: PayrollNumberFormat.Format(5000000) = "5.000.000", không phải "5000000".
        var ast = N.Cmp("=", N.Col(PayrollColumnCodes.FlagBh), N.Const(5_000_000m));

        PayrollExcelFormulaTranslator.Translate(ast, Refs()).Should().Be("EXACT(I13,\"5.000.000\")");
    }

    [Fact]
    public void Comparing_text_against_a_numeric_cell_throws_instead_of_emitting_a_diverging_formula()
    {
        // Engine quy đổi số → chuỗi VN; Excel định dạng theo locale máy người dùng nên không thể
        // bảo đảm trùng → thà hỏng lúc xuất còn hơn ra file sai số.
        var ast = N.Cmp("=", N.Col(PayrollColumnCodes.FlagBh), N.Col(PayrollColumnCodes.MucLuong));

        var act = () => PayrollExcelFormulaTranslator.Translate(ast, Refs());

        act.Should().Throw<InvalidOperationException>().WithMessage("*so sánh chuỗi với một ô số*");
    }

    [Fact]
    public void Variadic_function_with_no_arguments_returns_zero_like_the_engine_not_invalid_syntax()
    {
        // Engine: SUM() = 0. Excel: "SUM()" là lỗi cú pháp → file hỏng.
        PayrollExcelFormulaTranslator.Translate(N.Func(PayrollFunctions.Sum), Refs()).Should().Be("0");
        PayrollExcelFormulaTranslator.Translate(N.Func(PayrollFunctions.Min), Refs()).Should().Be("0");
    }

    [Fact]
    public void Progressive_tax_throws_on_a_corrupted_bracket_table_instead_of_dropping_branches()
    {
        var broken = new List<PitBracketSnapshot>
        {
            new(1, 0m, null, 0.05m),              // thiếu cận trên nhưng không phải bậc cuối
            new(2, 5_000_000m, null, 0.10m),
        };
        var ast = N.Func(PayrollFunctions.ProgressiveTax, N.Col(PayrollColumnCodes.Tntt));

        var act = () => PayrollExcelFormulaTranslator.Translate(ast, Refs(broken));

        act.Should().Throw<InvalidOperationException>().WithMessage("*Biểu thuế của kỳ hỏng*");
    }
}
