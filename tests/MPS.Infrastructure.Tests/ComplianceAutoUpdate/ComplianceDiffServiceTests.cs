using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Enums.Compliance;
using MPS.Infrastructure.ComplianceAutoUpdate.Validation;
using MPS.Infrastructure.Persistence;

namespace MPS.Infrastructure.Tests.ComplianceAutoUpdate;

public sealed class ComplianceDiffServiceTests
{
    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SocialInsurance_FlattensFundsByFundCode_AndDetectsChange()
    {
        // Arrange — active version: quỹ HT 8/14
        await using var db = NewDb();
        db.SocialInsuranceRateVersions.Add(new SocialInsuranceRateVersion
        {
            ValidFrom = new DateOnly(2025, 7, 1),
            IsActive = true,
            EmployeeHealthPercent = 1.5m,
            EmployerHealthPercent = 3.0m,
            EmployeeUnemploymentPercent = 1.0m,
            EmployerUnemploymentPercent = 1.0m,
            EmployerUnionFundPercent = 2.0m,
            ReferenceAmount = 2_340_000m,
            EligibilityMode = "ByContractSubtype",
            LegalReference = "seed",
            CreatedByUserId = 1,
            Funds =
            {
                new SocialInsuranceFundEntry { FundCode = SocialInsuranceFundCodes.Ht, FundName = "HT", EmployeePercent = 8m, EmployerPercent = 14m },
            },
        });
        await db.SaveChangesAsync();

        var sut = new ComplianceDiffService(db, NullLogger<ComplianceDiffService>.Instance);

        // Proposed: HT employer đổi 14 → 15, employee giữ 8
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.SocialInsuranceRateVersion,
            Values =
            {
                new ExtractedValue { Field = "Fund:HT:Employee", Number = 8m },
                new ExtractedValue { Field = "Fund:HT:Employer", Number = 15m },
            },
        };
        var report = new ValidationReport();

        // Act
        var diff = await sut.ComputeDiffAsync(result, report, CancellationToken.None);

        // Assert
        diff.HasSubstantiveChange.Should().BeTrue();
        diff.Entries.Should().Contain(e =>
            e.Field == "Fund:HT:Employer" && e.ChangeType == ChangeType.Changed && e.OldValue == "14" && e.NewValue == "15");
        diff.Entries.Should().Contain(e => e.Field == "Fund:HT:Employee" && e.ChangeType == ChangeType.Unchanged);
    }

    [Fact]
    public async Task MinimumWage_NoChange_WhenProposedEqualsActive()
    {
        await using var db = NewDb();
        db.MinimumWageVersions.Add(new MinimumWageVersion
        {
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
            AppliedRegionCode = "I",
            LegalReference = "seed",
            CreatedByUserId = 1,
            Regions =
            {
                new MinimumWageRegion { RegionCode = "I", AmountPerMonth = 4_960_000m },
            },
        });
        await db.SaveChangesAsync();

        var sut = new ComplianceDiffService(db, NullLogger<ComplianceDiffService>.Instance);
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            Values = { new ExtractedValue { Field = "Region:I", Number = 4_960_000m } },
        };

        var diff = await sut.ComputeDiffAsync(result, new ValidationReport(), CancellationToken.None);

        diff.HasSubstantiveChange.Should().BeFalse();
    }

    [Fact]
    public async Task AbnormalJump_AddsWarning_WhenChangeOver30Percent()
    {
        await using var db = NewDb();
        db.PitRegulationVersions.Add(new PitRegulationVersion
        {
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true,
            PersonalDeductionAmount = 11_000_000m,
            DependentDeductionAmount = 4_400_000m,
            LegalReference = "seed",
            CreatedByUserId = 1,
        });
        await db.SaveChangesAsync();

        var sut = new ComplianceDiffService(db, NullLogger<ComplianceDiffService>.Instance);
        var report = new ValidationReport();
        var result = new ExtractionResult
        {
            TargetEntityType = TargetEntityTypes.PitRegulationVersion,
            // 11tr → 20tr là +81% > 30% → cảnh báo
            Values = { new ExtractedValue { Field = "PersonalDeductionAmount", Number = 20_000_000m } },
        };

        var diff = await sut.ComputeDiffAsync(result, report, CancellationToken.None);

        diff.HasSubstantiveChange.Should().BeTrue();
        report.WarningCount.Should().BeGreaterThan(0);
    }
}
