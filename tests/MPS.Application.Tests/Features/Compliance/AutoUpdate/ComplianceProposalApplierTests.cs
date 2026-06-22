using FluentAssertions;
using MediatR;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.AutoUpdate.Approval;
using MPS.Application.Features.Compliance.MinimumWage.Commands;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.Compliance.AutoUpdate;

public sealed class ComplianceProposalApplierTests
{
    private readonly Mock<IMinimumWageRepository> _mw = new();
    private readonly Mock<ISocialInsuranceRateRepository> _si = new();
    private readonly Mock<IPitRegulationRepository> _pit = new();
    private readonly Mock<ILaborLawConstantsRepository> _ll = new();
    private readonly Mock<ILegalDocumentRepository> _legalDoc = new();
    private readonly Mock<IComplianceAuthoritativeSourceRepository> _authoritative = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<ISender> _sender = new();
    private readonly Mock<IClock> _clock = new();

    private ComplianceProposalApplier CreateSut()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc));
        _currentUser.SetupGet(u => u.UserId).Returns(1);
        _legalDoc.Setup(r => r.GetByCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MPS.Domain.Entities.Compliance.LegalDocument?)null);
        _legalDoc.Setup(r => r.AddAsync(It.IsAny<MPS.Domain.Entities.Compliance.LegalDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);
        return new ComplianceProposalApplier(
            _mw.Object, _si.Object, _pit.Object, _ll.Object,
            _legalDoc.Object, _authoritative.Object, _currentUser.Object, _sender.Object, _clock.Object);
    }

    [Fact]
    public async Task ApplyAsync_MinimumWage_OverlaysProposedRegion_KeepsOthers_AndCreatesPendingVersion()
    {
        // Arrange — active có 4 vùng; proposal đổi mỗi Region:I
        _mw.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MinimumWageVersion
        {
            Id = 5,
            AppliedRegionCode = "I",
            Regions =
            {
                new MinimumWageRegion { RegionCode = "I", AmountPerMonth = 4_960_000m },
                new MinimumWageRegion { RegionCode = "II", AmountPerMonth = 4_410_000m },
                new MinimumWageRegion { RegionCode = "III", AmountPerMonth = 3_860_000m },
                new MinimumWageRegion { RegionCode = "IV", AmountPerMonth = 3_450_000m },
            },
        });

        CreateMinimumWageCommand? captured = null;
        _sender.Setup(s => s.Send(It.IsAny<CreateMinimumWageCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<int>, CancellationToken>((c, _) => captured = (CreateMinimumWageCommand)c)
            .ReturnsAsync(99);

        var proposal = new ComplianceUpdateProposal
        {
            Id = 1,
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            ProposedValidFrom = new DateOnly(2027, 1, 1),
            ExtractedJson = """
            { "targetEntityType":"MinimumWageVersion",
              "values":[ { "field":"Region:I", "number":5200000 } ] }
            """,
        };

        // Act
        var newId = await CreateSut().ApplyAsync(proposal, CancellationToken.None);

        // Assert
        newId.Should().Be(99);
        captured.Should().NotBeNull();
        captured!.ApplyImmediately.Should().BeFalse();                    // không áp ngay
        captured.ValidFrom.Should().Be(new DateOnly(2027, 1, 1));         // theo đề xuất
        captured.Regions.Single(r => r.RegionCode == "I").AmountPerMonth.Should().Be(5_200_000m); // overlay
        captured.Regions.Single(r => r.RegionCode == "II").AmountPerMonth.Should().Be(4_410_000m); // giữ nguyên
    }

    [Fact]
    public async Task ApplyAsync_PastEffectiveDate_AppliesImmediately()
    {
        // Luật đã có hiệu lực trong quá khứ (vd ND 74/2024 hiệu lực 2024-07-01) → phải kích hoạt ngay,
        // không tạo version chờ hiệu lực (tránh lỗi "ValidFrom must be strictly after today").
        _mw.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MinimumWageVersion
        {
            Id = 5,
            AppliedRegionCode = "I",
            Regions =
            {
                new MinimumWageRegion { RegionCode = "I", AmountPerMonth = 4_500_000m },
                new MinimumWageRegion { RegionCode = "II", AmountPerMonth = 4_000_000m },
                new MinimumWageRegion { RegionCode = "III", AmountPerMonth = 3_500_000m },
                new MinimumWageRegion { RegionCode = "IV", AmountPerMonth = 3_100_000m },
            },
        });

        CreateMinimumWageCommand? captured = null;
        _sender.Setup(s => s.Send(It.IsAny<CreateMinimumWageCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<int>, CancellationToken>((c, _) => captured = (CreateMinimumWageCommand)c)
            .ReturnsAsync(77);

        var proposal = new ComplianceUpdateProposal
        {
            Id = 9,
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            ProposedValidFrom = new DateOnly(2024, 7, 1), // quá khứ so với clock 2026-06-18
            ExtractedJson = """{ "values":[ { "field":"Region:I", "number":4960000 } ] }""",
        };

        await CreateSut().ApplyAsync(proposal, CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ApplyImmediately.Should().BeTrue();
        captured.ValidFrom.Should().Be(new DateOnly(2024, 7, 1));
    }

    [Fact]
    public async Task ApplyAsync_Throws_WhenNoActiveBaseVersion()
    {
        _mw.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync((MinimumWageVersion?)null);

        var proposal = new ComplianceUpdateProposal
        {
            Id = 2,
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            ExtractedJson = """{ "values":[] }""",
        };

        var act = () => CreateSut().ApplyAsync(proposal, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task ApplyAsync_Throws_ForUnsupportedTargetType()
    {
        var proposal = new ComplianceUpdateProposal
        {
            Id = 3,
            TargetEntityType = TargetEntityTypes.AllowanceExemptThresholdVersion,
            ExtractedJson = """{ "values":[] }""",
        };

        var act = () => CreateSut().ApplyAsync(proposal, CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }
}
