using FluentAssertions;
using MediatR;
using Moq;
using MPS.Application.Behaviors;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Tests.Behaviors;

/// <summary>
/// Unit test cho pipeline behavior chặn write trên HĐ archived.
/// Sử dụng request giả implement <see cref="IContractScopedCommand"/> để cô lập behavior.
/// </summary>
public sealed class ArchivedContractGuardBehaviorTests
{
    /// <summary>
    /// HĐ KHÔNG archived → behavior cho qua, gọi <c>next()</c> bình thường.
    /// </summary>
    [Fact]
    public async Task Calls_Next_When_Contract_Is_Not_Archived()
    {
        var repo = new Mock<IContractRepository>();
        repo.Setup(r => r.GetArchivedAtAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var behavior = new ArchivedContractGuardBehavior<FakeScopedCommand, string>(repo.Object);
        var request = new FakeScopedCommand(42);

        var result = await behavior.Handle(
            request,
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
        repo.Verify(r => r.GetArchivedAtAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// HĐ ĐÃ archived → behavior throw ContractArchivedException, KHÔNG gọi next.
    /// </summary>
    [Fact]
    public async Task Throws_ContractArchivedException_When_ArchivedAt_Not_Null()
    {
        var repo = new Mock<IContractRepository>();
        repo.Setup(r => r.GetArchivedAtAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTime.UtcNow);

        var behavior = new ArchivedContractGuardBehavior<FakeScopedCommand, string>(repo.Object);
        var nextCalled = false;

        var act = () => behavior.Handle(
            new FakeScopedCommand(99),
            () => { nextCalled = true; return Task.FromResult("should-not-run"); },
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<ContractArchivedException>();
        ex.Which.ContractId.Should().Be(99);
        ex.Which.Message.Should().Contain("CONTRACT_ARCHIVED");
        nextCalled.Should().BeFalse("next must not run after guard throws");
    }

    /// <summary>
    /// Request KHÔNG implement IContractScopedCommand → behavior bypass, không query DB.
    /// </summary>
    [Fact]
    public async Task Skips_Lookup_When_Request_Is_Not_ContractScoped()
    {
        var repo = new Mock<IContractRepository>();

        var behavior = new ArchivedContractGuardBehavior<FakeNonScopedRequest, int>(repo.Object);

        var result = await behavior.Handle(
            new FakeNonScopedRequest(),
            () => Task.FromResult(123),
            CancellationToken.None);

        result.Should().Be(123);
        repo.Verify(r => r.GetArchivedAtAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Request implement marker nhưng ContractId &lt;= 0 → skip lookup (input không hợp lệ,
    /// downstream validator sẽ reject).
    /// </summary>
    [Fact]
    public async Task Skips_Lookup_When_ContractId_Is_Zero_Or_Negative()
    {
        var repo = new Mock<IContractRepository>();

        var behavior = new ArchivedContractGuardBehavior<FakeScopedCommand, string>(repo.Object);

        var result = await behavior.Handle(
            new FakeScopedCommand(0),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        result.Should().Be("ok");
        repo.Verify(r => r.GetArchivedAtAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed record FakeScopedCommand(int ContractId) : IRequest<string>, IContractScopedCommand;

    private sealed record FakeNonScopedRequest : IRequest<int>;
}
