using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Infrastructure.ComplianceAutoUpdate.Validation;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

public sealed class ComplianceLogicValidatorTests
{
    private readonly ComplianceLogicValidator _sut = new(NullLogger<ComplianceLogicValidator>.Instance);

    [Fact]
    public void MinimumWage_Errors_WhenRegionOrderViolated()
    {
        // vùng I (4tr) <= vùng II (5tr) → sai thứ tự
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            Values =
            {
                new ExtractedValue { Field = "Region:I", Number = 4_000_000m },
                new ExtractedValue { Field = "Region:II", Number = 5_000_000m },
                new ExtractedValue { Field = "Region:III", Number = 3_500_000m },
                new ExtractedValue { Field = "Region:IV", Number = 3_200_000m },
            },
        };
        var report = new ValidationReport();

        _sut.Validate(result, report);

        report.HasError.Should().BeTrue();
    }

    [Fact]
    public void MinimumWage_Passes_WhenFourRegionsDescending()
    {
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            Values =
            {
                new ExtractedValue { Field = "Region:I", Number = 4_960_000m },
                new ExtractedValue { Field = "Region:II", Number = 4_410_000m },
                new ExtractedValue { Field = "Region:III", Number = 3_860_000m },
                new ExtractedValue { Field = "Region:IV", Number = 3_450_000m },
            },
        };
        var report = new ValidationReport();

        _sut.Validate(result, report);

        report.HasError.Should().BeFalse();
    }

    [Fact]
    public void Pit_Errors_WhenRateNotIncreasing()
    {
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.PitRegulationVersion,
            Values =
            {
                new ExtractedValue { Field = "Bracket:1:Rate", Number = 0.05m },
                new ExtractedValue { Field = "Bracket:2:Rate", Number = 0.05m }, // không tăng
            },
        };
        var report = new ValidationReport();

        _sut.Validate(result, report);

        report.HasError.Should().BeTrue();
    }

    [Fact]
    public void Social_Warns_OnAbnormallyHighRate_ButReferenceAmountIgnored()
    {
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.SocialInsuranceRateVersion,
            Values =
            {
                new ExtractedValue { Field = "Fund:HT:Employer", Number = 99m },          // cao bất thường → warn
                new ExtractedValue { Field = "ReferenceAmount", Number = 2_530_000m },     // tiền, không bị coi là %
            },
        };
        var report = new ValidationReport();

        _sut.Validate(result, report);

        report.WarningCount.Should().Be(1);
        report.HasError.Should().BeFalse();
    }
}
