using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Application.Features.SalaryGrade.Validators;
using MPS.Domain.Interfaces;

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

    // ===== Ràng buộc bậc lương tăng dần trong cùng một chức danh =====

    [Fact]
    public async Task Update_BaseSalary_Not_Greater_Than_Lower_Level_Is_Rejected()
    {
        var current = new MPS.Domain.Entities.SalaryGrade
        {
            Id = 1, PositionId = 1, Level = 3, BaseSalary = 12_000_000m, Currency = "VND", IsActive = true,
        };
        var siblings = new[]
        {
            current,
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 2, PositionId = 1, Level = 2, BaseSalary = 8_000_000m, Currency = "VND", IsActive = true,
            },
        };
        var validator = NewValidator(current: current, siblings: siblings);

        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 7_000_000m });

        result.ShouldHaveValidationErrorFor("BaseSalary")
            .WithErrorMessage("Lương bậc 3 (7,000,000 đ) phải lớn hơn lương bậc 2 (8,000,000 đ).");
    }

    [Fact]
    public async Task Update_BaseSalary_Not_Less_Than_Upper_Level_Is_Rejected()
    {
        var current = new MPS.Domain.Entities.SalaryGrade
        {
            Id = 1, PositionId = 1, Level = 2, BaseSalary = 8_000_000m, Currency = "VND", IsActive = true,
        };
        var siblings = new[]
        {
            current,
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 2, PositionId = 1, Level = 3, BaseSalary = 12_000_000m, Currency = "VND", IsActive = true,
            },
        };
        var validator = NewValidator(current: current, siblings: siblings);

        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 15_000_000m });

        result.ShouldHaveValidationErrorFor("BaseSalary")
            .WithErrorMessage("Lương bậc 2 (15,000,000 đ) phải nhỏ hơn lương bậc 3 (12,000,000 đ).");
    }

    [Fact]
    public async Task Update_BaseSalary_Strictly_Between_Neighbors_Is_Accepted()
    {
        var current = new MPS.Domain.Entities.SalaryGrade
        {
            Id = 1, PositionId = 1, Level = 2, BaseSalary = 8_000_000m, Currency = "VND", IsActive = true,
        };
        var siblings = new[]
        {
            current,
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 2, PositionId = 1, Level = 1, BaseSalary = 6_000_000m, Currency = "VND", IsActive = true,
            },
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 3, PositionId = 1, Level = 3, BaseSalary = 12_000_000m, Currency = "VND", IsActive = true,
            },
        };
        var validator = NewValidator(current: current, siblings: siblings);

        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 9_000_000m });

        result.ShouldNotHaveValidationErrorFor("BaseSalary");
    }

    private static SalaryGradeUpdateCommandValidator NewValidator(
        MPS.Domain.Entities.SalaryGrade? current = null,
        IReadOnlyList<MPS.Domain.Entities.SalaryGrade>? siblings = null)
    {
        var salaryRepo = new Mock<ISalaryGradeRepository>();
        salaryRepo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);
        salaryRepo.Setup(r => r.GetAllAsync(It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(siblings ?? Array.Empty<MPS.Domain.Entities.SalaryGrade>());

        return new SalaryGradeUpdateCommandValidator(
            salaryRepo.Object,
            SalaryGradeCreateCommandValidatorTests.NewComplianceLookup());
    }

    private static SalaryGradeUpdateCommand ValidCmd() => new(
        Id: 1,
        Name: "Senior Engineer Bậc 3",
        BaseSalary: 25_000_000m,
        Currency: "VND",
        Description: null,
        IsActive: true);
}
