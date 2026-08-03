using FluentValidation.TestHelper;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Application.Features.SalaryGrade.Validators;

namespace MPS.Application.Tests.Features.SalaryGrade;

/// <summary>
/// Nhánh SỬA bậc lương phải chịu cùng ràng buộc lương tối thiểu vùng như nhánh TẠO — nếu không,
/// chỉ cần tạo đúng luật rồi sửa xuống là lách được (Điều 90 BLLĐ 2019).
/// </summary>
public sealed class SalaryGradeUpdateCommandValidatorTests
{
    [Fact]
    public async Task BaseSalary_Below_Region_Minimum_Wage_Is_Rejected()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 1_000m });

        result.ShouldHaveValidationErrorFor("BaseSalary");
    }

    [Fact]
    public async Task BaseSalary_At_Or_Above_Region_Minimum_Wage_Is_Accepted()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 5_310_000m });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Foreign_Currency_Skips_Minimum_Wage_Check()
    {
        var validator = NewValidator();
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 500m, Currency = "USD" });

        result.ShouldNotHaveValidationErrorFor("BaseSalary");
    }

    private static SalaryGradeUpdateCommandValidator NewValidator() =>
        new(SalaryGradeCreateCommandValidatorTests.NewComplianceLookup());

    private static SalaryGradeUpdateCommand ValidCmd() => new(
        Id: 1,
        Name: "Senior Engineer Bậc 3",
        BaseSalary: 25_000_000m,
        Currency: "VND",
        Description: null,
        IsActive: true);
}
