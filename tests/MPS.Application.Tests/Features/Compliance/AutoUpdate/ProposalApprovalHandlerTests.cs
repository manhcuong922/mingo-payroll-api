using FluentAssertions;
using MediatR;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.AutoUpdate.Approval;
using MPS.Domain.Entities.Compliance;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.Compliance.AutoUpdate;

public sealed class ProposalApprovalHandlerTests
{
    private readonly Mock<IComplianceUpdateProposalRepository> _repo = new();
    private readonly Mock<IClock> _clock = new();

    public ProposalApprovalHandlerTests() =>
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 6, 18, 0, 0, 0, DateTimeKind.Utc));

    private ComplianceProposalApplier RealApplier(Mock<ISender> sender, Mock<IMinimumWageRepository> mw) =>
        new(mw.Object, Mock.Of<ISocialInsuranceRateRepository>(), Mock.Of<IPitRegulationRepository>(),
            Mock.Of<ILaborLawConstantsRepository>(), sender.Object, _clock.Object);

    [Fact]
    public async Task Approve_SetsApproved_AndCreatesVersion()
    {
        var proposal = new ComplianceUpdateProposal
        {
            Id = 1,
            Status = ProposalStatuses.PendingReview,
            TargetEntityType = TargetEntityTypes.MinimumWageVersion,
            ProposedValidFrom = new DateOnly(2027, 1, 1),
            ExtractedJson = """{ "values":[ { "field":"Region:I", "number":5000000 } ] }""",
        };
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);

        var mw = new Mock<IMinimumWageRepository>();
        mw.Setup(r => r.GetActiveAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new MinimumWageVersion
        {
            AppliedRegionCode = "I",
            Regions =
            {
                new MinimumWageRegion { RegionCode = "I", AmountPerMonth = 4_960_000m },
                new MinimumWageRegion { RegionCode = "II", AmountPerMonth = 4_410_000m },
                new MinimumWageRegion { RegionCode = "III", AmountPerMonth = 3_860_000m },
                new MinimumWageRegion { RegionCode = "IV", AmountPerMonth = 3_450_000m },
            },
        });
        var sender = new Mock<ISender>();
        sender.Setup(s => s.Send(It.IsAny<MPS.Application.Features.Compliance.MinimumWage.Commands.CreateMinimumWageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(77);

        var sut = new ApproveComplianceProposalCommandHandler(_repo.Object, RealApplier(sender, mw), _clock.Object);

        var result = await sut.Handle(new ApproveComplianceProposalCommand(1, "duyệt OK"), CancellationToken.None);

        result.NewVersionId.Should().Be(77);
        result.Status.Should().Be(ProposalStatuses.Approved);
        proposal.Status.Should().Be(ProposalStatuses.Approved);
        proposal.ReviewNote.Should().Be("duyệt OK");
        proposal.ReviewedAt.Should().NotBeNull();
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Approve_Throws_WhenNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((ComplianceUpdateProposal?)null);
        var sut = new ApproveComplianceProposalCommandHandler(
            _repo.Object, RealApplier(new Mock<ISender>(), new Mock<IMinimumWageRepository>()), _clock.Object);

        var act = () => sut.Handle(new ApproveComplianceProposalCommand(404, null), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Approve_Throws_WhenNotPending()
    {
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new ComplianceUpdateProposal
        {
            Id = 1, Status = ProposalStatuses.Approved,
        });
        var sut = new ApproveComplianceProposalCommandHandler(
            _repo.Object, RealApplier(new Mock<ISender>(), new Mock<IMinimumWageRepository>()), _clock.Object);

        var act = () => sut.Handle(new ApproveComplianceProposalCommand(1, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Reject_SetsRejected_WithoutCreatingVersion()
    {
        var proposal = new ComplianceUpdateProposal { Id = 1, Status = ProposalStatuses.PendingReview };
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(proposal);

        var sut = new RejectComplianceProposalCommandHandler(_repo.Object, _clock.Object);

        var result = await sut.Handle(new RejectComplianceProposalCommand(1, "không chính xác"), CancellationToken.None);

        result.Status.Should().Be(ProposalStatuses.Rejected);
        result.NewVersionId.Should().BeNull();
        proposal.Status.Should().Be(ProposalStatuses.Rejected);
        proposal.ReviewNote.Should().Be("không chính xác");
        _repo.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
