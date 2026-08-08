using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Features.Compliance.AutoUpdate.Abstractions;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Options;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.Compliance.AutoUpdate;

/// <summary>
/// Test orchestration của lõi dùng chung: scope → extract → validate → diff → (tạo) proposal.
/// Mock các collaborator để lái từng nhánh; không phụ thuộc cài đặt extractor/diff thật.
/// </summary>
public sealed class ComplianceDocumentProcessorTests
{
    private readonly Mock<IContentScopeFilter> _scope = new();
    private readonly Mock<IComplianceExtractor> _extractor = new();
    private readonly Mock<ISourceGroundingValidator> _grounding = new();
    private readonly Mock<IComplianceLogicValidator> _logic = new();
    private readonly Mock<IComplianceDiffService> _diff = new();
    private readonly Mock<IComplianceUpdateProposalRepository> _proposals = new();

    private readonly ComplianceAutoUpdateOptions _options = new();

    private ComplianceDocumentProcessor CreateSut() => new(
        _scope.Object, _extractor.Object, _grounding.Object, _logic.Object, _diff.Object,
        _proposals.Object, _options, NullLogger<ComplianceDocumentProcessor>.Instance);

    private static RawDocument Doc(bool dryRun = false) =>
        new("manual-upload", null, "74/2024/NĐ-CP", "toàn văn ...", dryRun);

    private void SetupScoped(bool hasContent) =>
        _scope.Setup(s => s.Filter(It.IsAny<string>())).Returns(new ScopedContent
        {
            SourceText = "src",
            TotalClauses = hasContent ? 1 : 0,
            Clauses = hasContent ? new() { new ScopedClause(3, "nội dung") } : new(),
        });

    private static ExtractionResult SampleResult() => new()
    {
        TargetEntityType = TargetEntityTypes.MinimumWageVersion,
        Values = { new ExtractedValue { Field = "Region:I", Number = 5_000_000m, Evidence = "vùng I" } },
        RawJson = "{}",
    };

    private void SetupDiff(bool hasChange) =>
        _diff.Setup(d => d.ComputeDiffAsync(It.IsAny<ExtractionResult>(), It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var r = new DiffResult();
                if (hasChange) r.Entries.Add(new DiffEntry("Region:I", "4960000", "5000000", ChangeType.Changed));
                return r;
            });

    /// <summary>Diff kiểu "AI bóc hụt": data active có <paramref name="activeFields"/> field,
    /// AI chỉ lấy lại được <paramref name="covered"/> field, phần còn lại thành Removed.</summary>
    private void SetupPartialDiff(int activeFields, int covered) =>
        _diff.Setup(d => d.ComputeDiffAsync(It.IsAny<ExtractionResult>(), It.IsAny<ValidationReport>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                var r = new DiffResult();
                for (var i = 0; i < activeFields; i++)
                {
                    r.Entries.Add(i < covered
                        ? new DiffEntry($"F{i}", "1", "2", ChangeType.Changed)
                        : new DiffEntry($"F{i}", "1", null, ChangeType.Removed));
                }
                return r;
            });

    [Fact]
    public async Task ProcessAsync_WrongEntityType_ReturnsFailed_AndDoesNotPersist()
    {
        SetupScoped(hasContent: true);
        // Caller đang rà ngưỡng phụ cấp, AI phán văn bản là hằng số lao động.
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExtractionResult
            {
                TargetEntityType = TargetEntityTypes.LaborLawConstantsVersion,
                RawJson = "{}",
            });
        SetupDiff(hasChange: true);

        var doc = Doc() with { ExpectedEntityType = TargetEntityTypes.AllowanceExemptThresholdVersion };
        var outcome = await CreateSut().ProcessAsync(doc, CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Failed);
        _proposals.Verify(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ExtractionMissesMostActiveFields_ReturnsFailed_AndDoesNotPersist()
    {
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        // 13 field đang áp dụng, AI không lấy lại được field nào — đúng ca proposal rác gặp thật.
        SetupPartialDiff(activeFields: 13, covered: 0);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Failed);
        outcome.Reason.Should().Contain("0/13");
        _proposals.Verify(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ExtractionCoversMostActiveFields_StillCreatesProposal()
    {
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        // Bóc lại 9/10 field: bãi bỏ thật 1 field, không được chặn nhầm.
        SetupPartialDiff(activeFields: 10, covered: 9);
        _proposals.Setup(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Proposal);
    }

    [Fact]
    public async Task ProcessAsync_CoverageGuardDisabled_CreatesProposalEvenWhenExtractionMissesFields()
    {
        _options.MinActiveFieldCoverage = 0;
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        SetupPartialDiff(activeFields: 13, covered: 0);
        _proposals.Setup(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>())).ReturnsAsync(9);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Proposal);
    }

    [Fact]
    public async Task ProcessAsync_NoScopedContent_ReturnsNoScopedContent_AndDoesNotPersist()
    {
        SetupScoped(hasContent: false);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.NoScopedContent);
        _proposals.Verify(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ExtractorReturnsNull_ReturnsFailed()
    {
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExtractionResult?)null);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Failed);
    }

    [Fact]
    public async Task ProcessAsync_NoSubstantiveChange_ReturnsNoChange_AndDoesNotPersist()
    {
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        SetupDiff(hasChange: false);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.NoChange);
        _proposals.Verify(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_SubstantiveChange_CreatesProposal()
    {
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        SetupDiff(hasChange: true);
        _proposals.Setup(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>())).ReturnsAsync(123);

        var outcome = await CreateSut().ProcessAsync(Doc(), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Proposal);
        outcome.ProposalId.Should().Be(123);
        _proposals.Verify(p => p.AddAsync(
            It.Is<ComplianceUpdateProposal>(x => x.SourceKey == "manual-upload" && x.Status == ProposalStatuses.PendingReview),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DryRun_DetectsChange_ButDoesNotPersist()
    {
        SetupScoped(hasContent: true);
        _extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(SampleResult());
        SetupDiff(hasChange: true);

        var outcome = await CreateSut().ProcessAsync(Doc(dryRun: true), CancellationToken.None);

        outcome.Kind.Should().Be(IngestionKind.Proposal);
        outcome.ProposalId.Should().BeNull();
        _proposals.Verify(p => p.AddAsync(It.IsAny<ComplianceUpdateProposal>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
