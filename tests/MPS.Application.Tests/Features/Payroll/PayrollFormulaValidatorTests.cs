using FluentAssertions;
using MPS.Application.Common.Payroll;
using MPS.Application.Common.Payroll.Formula;
using MPS.Domain.Constants;
using MPS.Domain.Entities.Payroll;
using MPS.Domain.Enums.Payroll;

namespace MPS.Application.Tests.Features.Payroll;

public sealed class PayrollFormulaValidatorTests
{
    private static readonly string[] NoParams = Array.Empty<string>();

    private static PayrollFormulaValidator NewValidator() =>
        new(new PayrollFieldCatalog(), new FormulaFunctionRegistry(), new ColumnDependencyGraph());

    private static SalaryColumnDefinition Input(string code) => new()
    {
        ColumnCode = code, Label = code, ValueSource = ColumnValueSource.Input, DataType = ColumnDataType.Number,
    };

    private static SalaryColumnDefinition Formula(string code, FormulaNode node) => new()
    {
        ColumnCode = code, Label = code, ValueSource = ColumnValueSource.Formula, DataType = ColumnDataType.Money,
        FormulaJson = PayrollJson.Serialize(node),
    };

    private static SalaryColumnDefinition Lookup(string code, string lookupKey) => new()
    {
        ColumnCode = code, Label = code, ValueSource = ColumnValueSource.Lookup, DataType = ColumnDataType.Money,
        LookupKey = lookupKey,
    };

    [Fact]
    public void Validate_ValidSet_ReturnsNoErrors()
    {
        var columns = new List<SalaryColumnDefinition>
        {
            Input("MUCLUONG"),
            Formula("DOUBLE", FormulaNode.Mul(FormulaNode.Col("MUCLUONG"), FormulaNode.Const(2))),
            Lookup("HESO", PayrollFieldKeys.ProductivityCoefficient),
        };

        NewValidator().Validate(columns, NoParams).Should().BeEmpty();
    }

    [Fact]
    public void Validate_CyclicColumns_ReportsCycle()
    {
        var columns = new List<SalaryColumnDefinition>
        {
            Formula("A", FormulaNode.Col("B")),
            Formula("B", FormulaNode.Col("A")),
        };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().ContainAny("vòng", "chu trình");
    }

    [Fact]
    public void Validate_UnknownField_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Formula("X", FormulaNode.Field("khong.ton.tai")) };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().Contain("field không tồn tại");
    }

    [Fact]
    public void Validate_UnknownFunction_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Formula("X", FormulaNode.Func("KHONGCO", FormulaNode.Const(1))) };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().Contain("hàm không tồn tại");
    }

    [Fact]
    public void Validate_FunctionWrongArity_ReportsError()
    {
        // ROUND cần 2 tham số, chỉ truyền 1.
        var columns = new List<SalaryColumnDefinition>
        {
            Formula("X", FormulaNode.Func(PayrollFunctions.Round, FormulaNode.Const(1))),
        };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().Contain("cần 2 tham số");
    }

    [Fact]
    public void Validate_UnknownParam_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Formula("X", FormulaNode.Param("NOPE")) };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().Contain("tham số công ty không tồn tại");
    }

    [Fact]
    public void Validate_KnownParam_Passes()
    {
        var columns = new List<SalaryColumnDefinition>
        {
            Formula("X", FormulaNode.Mul(FormulaNode.Const(2), FormulaNode.Param("MEAL_PRICE_PER_WORKDAY"))),
        };

        NewValidator().Validate(columns, new[] { "MEAL_PRICE_PER_WORKDAY" }).Should().BeEmpty();
    }

    [Fact]
    public void Validate_UnknownColumnRef_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Formula("X", FormulaNode.Col("GHOST")) };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().Contain("cột không tồn tại");
    }

    [Fact]
    public void Validate_DuplicateColumnCode_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Input("DUP"), Input("DUP") };

        NewValidator().Validate(columns, NoParams)
            .Should().Contain(e => e.Contains("trùng"));
    }

    [Fact]
    public void Validate_InvalidColumnCode_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Input("1BAD") };

        NewValidator().Validate(columns, NoParams)
            .Should().Contain(e => e.Contains("không hợp lệ"));
    }

    [Fact]
    public void Validate_LookupUnknownKey_ReportsError()
    {
        var columns = new List<SalaryColumnDefinition> { Lookup("X", "bogus.key") };

        NewValidator().Validate(columns, NoParams)
            .Should().ContainSingle().Which.Should().Contain("field không tồn tại");
    }
}
