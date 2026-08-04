using FluentValidation.TestHelper;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models.Compliance;
using MPS.Application.Features.SalaryGrade.Commands;
using MPS.Application.Features.SalaryGrade.Validators;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.SalaryGrade;

/// <summary>
/// Unit test cho validator tạo SalaryGrade — cover các rule sau khi Code do BE tự sinh.
/// </summary>
public sealed class SalaryGradeCreateCommandValidatorTests
{
    [Fact]
    public async Task PositionId_Must_Be_Positive()
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { PositionId = 0 });

        result.ShouldHaveValidationErrorFor(c => c.PositionId)
            .WithErrorMessage("PositionId is required.");
    }

    [Fact]
    public async Task Missing_Position_Is_Rejected()
    {
        var validator = NewValidator(positionExists: false);
        var result = await validator.TestValidateAsync(ValidCmd());

        result.ShouldHaveValidationErrorFor(c => c.PositionId)
            .WithErrorMessage("Position does not exist.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Level_Must_Be_Positive(int level)
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { Level = level });

        result.ShouldHaveValidationErrorFor(c => c.Level)
            .WithErrorMessage("Level must be greater than 0.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1000)]
    public async Task BaseSalary_Must_Be_Positive(decimal salary)
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = salary });

        result.ShouldHaveValidationErrorFor(c => c.BaseSalary)
            .WithErrorMessage("BaseSalary must be greater than 0.");
    }

    [Theory]
    [InlineData("VN")]    // chỉ 2 chữ
    [InlineData("VNDX")]  // 4 chữ
    [InlineData("12A")]   // có digit
    public async Task Currency_Must_Be_3_Letter_ISO4217(string currency)
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { Currency = currency });

        result.ShouldHaveValidationErrorFor(c => c.Currency);
    }

    [Fact]
    public async Task Valid_Command_Passes_All_Rules()
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd());

        result.ShouldNotHaveAnyValidationErrors();
    }

    // ===== Lương tối thiểu vùng (Điều 90 BLLĐ 2019) =====

    [Fact]
    public async Task BaseSalary_Below_Region_Minimum_Wage_Is_Rejected()
    {
        // Bậc lương là nguồn của Contract.SalaryAmountSnapshot ⇒ số dưới luật chảy thẳng vào
        // hợp đồng, nền đóng BHXH và bảng lương nếu không chặn ở đây.
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 1_000m });

        result.ShouldHaveValidationErrorFor("BaseSalary");
    }

    [Fact]
    public async Task BaseSalary_Exactly_At_Region_Minimum_Wage_Is_Accepted()
    {
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = AppliedMinimumWage });

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public async Task Foreign_Currency_Skips_Minimum_Wage_Check()
    {
        // Bậc lương ngoại tệ không so trực tiếp được với mức tối thiểu VND (cần tỷ giá tại thời
        // điểm chi trả) — rule chỉ áp cho VND.
        var validator = NewValidator(positionExists: true);
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 500m, Currency = "USD" });

        result.ShouldNotHaveValidationErrorFor("BaseSalary");
    }

    [Fact]
    public async Task Missing_Compliance_Version_Skips_Rule_Instead_Of_Throwing()
    {
        // Chưa kích hoạt phiên bản lương tối thiểu nào → bỏ qua rule, KHÔNG ném 500 làm khoá
        // luôn màn hình master data.
        var validator = NewValidator(positionExists: true, hasActiveMinimumWage: false);
        var result = await validator.TestValidateAsync(ValidCmd() with { BaseSalary = 1_000m });

        result.ShouldNotHaveValidationErrorFor("BaseSalary");
    }

    // ===== Ràng buộc bậc lương tăng dần trong cùng một chức danh =====

    [Fact]
    public async Task BaseSalary_Not_Greater_Than_Lower_Level_Is_Rejected()
    {
        var siblings = new[]
        {
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 10, PositionId = 1, Level = 2, BaseSalary = 8_000_000m, Currency = "VND", IsActive = true,
            },
        };
        var validator = NewValidator(positionExists: true, siblings: siblings);

        var result = await validator.TestValidateAsync(ValidCmd() with { Level = 3, BaseSalary = 8_000_000m });

        result.ShouldHaveValidationErrorFor("BaseSalary")
            .WithErrorMessage("Lương bậc 3 (8,000,000 đ) phải lớn hơn lương bậc 2 (8,000,000 đ).");
    }

    [Fact]
    public async Task BaseSalary_Not_Less_Than_Upper_Level_Is_Rejected()
    {
        var siblings = new[]
        {
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 20, PositionId = 1, Level = 3, BaseSalary = 15_000_000m, Currency = "VND", IsActive = true,
            },
        };
        var validator = NewValidator(positionExists: true, siblings: siblings);

        var result = await validator.TestValidateAsync(ValidCmd() with { Level = 2, BaseSalary = 15_000_000m });

        result.ShouldHaveValidationErrorFor("BaseSalary")
            .WithErrorMessage("Lương bậc 2 (15,000,000 đ) phải nhỏ hơn lương bậc 3 (15,000,000 đ).");
    }

    [Fact]
    public async Task BaseSalary_Strictly_Between_Neighbors_Is_Accepted()
    {
        var siblings = new[]
        {
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 30, PositionId = 1, Level = 1, BaseSalary = 6_000_000m, Currency = "VND", IsActive = true,
            },
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 31, PositionId = 1, Level = 3, BaseSalary = 12_000_000m, Currency = "VND", IsActive = true,
            },
        };
        var validator = NewValidator(positionExists: true, siblings: siblings);

        var result = await validator.TestValidateAsync(ValidCmd() with { Level = 2, BaseSalary = 9_000_000m });

        result.ShouldNotHaveValidationErrorFor("BaseSalary");
    }

    [Fact]
    public async Task Different_Currency_Siblings_Are_Skipped_By_Monotonicity()
    {
        var siblings = new[]
        {
            new MPS.Domain.Entities.SalaryGrade
            {
                Id = 40, PositionId = 1, Level = 1, BaseSalary = 20_000m, Currency = "USD", IsActive = true,
            },
        };
        var validator = NewValidator(positionExists: true, siblings: siblings);

        // 5tr VND < 20k USD về số học nhưng khác tệ nên không so được — không được ném lỗi
        var result = await validator.TestValidateAsync(ValidCmd() with { Level = 2, BaseSalary = 6_000_000m });

        result.ShouldNotHaveValidationErrorFor("BaseSalary");
    }

    /// <summary>Vùng I theo Nghị định 74/2024 — khớp seed.</summary>
    private const decimal AppliedMinimumWage = 5_310_000m;

    private static SalaryGradeCreateCommandValidator NewValidator(
        bool positionExists,
        bool hasActiveMinimumWage = true,
        IReadOnlyList<MPS.Domain.Entities.SalaryGrade>? siblings = null)
    {
        var repo = new Mock<IPositionRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(positionExists
                ? new MPS.Domain.Entities.Position { Id = 1, Code = "SE", Name = "Senior Engineer" }
                : null);

        var salaryRepo = new Mock<ISalaryGradeRepository>();
        salaryRepo.Setup(r => r.GetAllAsync(It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(siblings ?? Array.Empty<MPS.Domain.Entities.SalaryGrade>());

        return new SalaryGradeCreateCommandValidator(
            repo.Object,
            salaryRepo.Object,
            NewComplianceLookup(hasActiveMinimumWage));
    }

    internal static IComplianceLookupService NewComplianceLookup(bool hasActiveMinimumWage = true)
    {
        var compliance = new Mock<IComplianceLookupService>();
        var setup = compliance.Setup(c => c.GetActiveMinimumWageAsync(It.IsAny<CancellationToken>()));
        if (hasActiveMinimumWage)
        {
            setup.ReturnsAsync(new MinimumWageSnapshot(
                VersionId: 1,
                ValidFrom: new DateOnly(2024, 7, 1),
                AppliedRegionCode: "I",
                Regions: new[]
                {
                    new MinimumWageRegionSnapshot("I", AppliedMinimumWage),
                    new MinimumWageRegionSnapshot("II", 4_730_000m),
                },
                LegalReference: "Nghị định 74/2024/NĐ-CP"));
        }
        else
        {
            setup.ThrowsAsync(new InvalidOperationException("No active MinimumWageVersion found."));
        }

        return compliance.Object;
    }

    private static SalaryGradeCreateCommand ValidCmd() => new(
        Name: "Senior Engineer Bậc 3",
        PositionId: 1,
        Level: 3,
        BaseSalary: 25_000_000m,
        Currency: "VND",
        Description: null);
}
