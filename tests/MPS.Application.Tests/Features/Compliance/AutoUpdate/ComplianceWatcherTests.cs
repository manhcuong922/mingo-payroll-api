using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Compliance.AutoUpdate.Ingestion;
using MPS.Application.Features.Compliance.AutoUpdate.Models;
using MPS.Application.Features.Compliance.AutoUpdate.Watcher;
using MPS.Domain.Entities.Compliance.AutoUpdate;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Features.Compliance.AutoUpdate;

/// <summary>
/// Test orchestration Kênh B: degrade khi web search lỗi, đẩy URL vào lõi, map outcome → Lead.
/// </summary>
public sealed class ComplianceWatcherTests
{
    private readonly Mock<IComplianceAuthoritativeSourceRepository> _authoritative = new();
    private readonly Mock<IWebSearchDiscoverer> _discoverer = new();
    private readonly Mock<IDocumentTextFetcher> _fetcher = new();
    private readonly Mock<IComplianceDocumentProcessor> _processor = new();
    private readonly Mock<IComplianceLeadRepository> _leads = new();
    private readonly Mock<IClock> _clock = new();

    public ComplianceWatcherTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 6, 20, 0, 0, 0, DateTimeKind.Utc));
        _authoritative.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new ComplianceAuthoritativeSource
            {
                TargetEntityType = TargetEntityTypes.MinimumWageVersion,
                DocumentNo = "38/2022/NĐ-CP",
                DocumentUrl = "https://example.gov/old",
                LegalDocumentId = 1,
            },
        });
    }

    private ComplianceWatcher CreateSut() => new(
        _authoritative.Object, _discoverer.Object, _fetcher.Object, _processor.Object,
        _leads.Object, _clock.Object, NullLogger<ComplianceWatcher>.Instance);

    private static DiscoveryResult Discovery(string? url) =>
        new("74/2024/NĐ-CP", new DateOnly(2024, 7, 1), url, "Nghi có bản mới hơn.");

    [Fact]
    public async Task RunOnce_WebSearchUnavailable_AddsNeedsHumanLead_NoFetch()
    {
        _discoverer.Setup(d => d.DiscoverNewerAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new WebSearchUnavailableException("proxy không hỗ trợ"));

        var summary = await CreateSut().RunOnceAsync(CancellationToken.None);

        summary.NeedsHuman.Should().Be(1);
        _leads.Verify(l => l.AddAsync(It.Is<ComplianceLead>(x => x.Status == LeadStatuses.NeedsHuman), It.IsAny<CancellationToken>()), Times.Once);
        _fetcher.Verify(f => f.FromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnce_NoNewer_ReVerifies_NoLead()
    {
        _discoverer.Setup(d => d.DiscoverNewerAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DiscoveryResult?)null);

        var summary = await CreateSut().RunOnceAsync(CancellationToken.None);

        summary.NoChange.Should().Be(1);
        _authoritative.Verify(r => r.UpsertAsync(
            TargetEntityTypes.MinimumWageVersion, 1, "38/2022/NĐ-CP", "https://example.gov/old",
            It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _leads.Verify(l => l.AddAsync(It.IsAny<ComplianceLead>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnce_UrlGroundedToProposal_AddsProposalLead()
    {
        _discoverer.Setup(d => d.DiscoverNewerAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Discovery("https://example.gov/74-2024"));
        _fetcher.Setup(f => f.FromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Điều 3. Vùng I: 4.960.000 đồng");
        _processor.Setup(p => p.ProcessAsync(It.IsAny<RawDocument>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IngestionOutcome.Proposal(123, new ValidationReport(), new DiffResult()));

        var summary = await CreateSut().RunOnceAsync(CancellationToken.None);

        summary.Proposals.Should().Be(1);
        _leads.Verify(l => l.AddAsync(
            It.Is<ComplianceLead>(x => x.Status == LeadStatuses.Proposal && x.ProposalId == 123),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RunOnce_UrlFetchEmpty_AddsNeedsHumanLead()
    {
        _discoverer.Setup(d => d.DiscoverNewerAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Discovery("https://example.gov/scan.pdf"));
        _fetcher.Setup(f => f.FromUrlAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var summary = await CreateSut().RunOnceAsync(CancellationToken.None);

        summary.NeedsHuman.Should().Be(1);
        _leads.Verify(l => l.AddAsync(It.Is<ComplianceLead>(x => x.Status == LeadStatuses.NeedsHuman), It.IsAny<CancellationToken>()), Times.Once);
        _processor.Verify(p => p.ProcessAsync(It.IsAny<RawDocument>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
