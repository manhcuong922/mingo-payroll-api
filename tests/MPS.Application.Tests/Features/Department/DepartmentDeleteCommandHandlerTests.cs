using FluentAssertions;
using Moq;
using MPS.Application.Features.Department.Commands;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Department;

public sealed class DepartmentDeleteCommandHandlerTests
{
    private readonly Mock<IDepartmentRepository> _repo = new();

    private DepartmentDeleteCommandHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task Handle_ArchivesDepartment_SoftDelete()
    {
        _repo.Setup(r => r.ArchiveAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().Handle(new DepartmentDeleteCommand(5), CancellationToken.None);

        result.Should().BeTrue();
        _repo.Verify(r => r.ArchiveAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenDepartmentNotFound()
    {
        _repo.Setup(r => r.ArchiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().Handle(new DepartmentDeleteCommand(404), CancellationToken.None);

        result.Should().BeFalse();
    }
}
