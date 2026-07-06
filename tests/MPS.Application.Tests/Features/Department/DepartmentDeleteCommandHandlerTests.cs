using FluentAssertions;
using Moq;
using MPS.Application.Features.Department.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.Department;

public sealed class DepartmentDeleteCommandHandlerTests
{
    private readonly Mock<IDepartmentRepository> _repo = new();
    private readonly Mock<IProjectRepository> _projects = new();

    private DepartmentDeleteCommandHandler CreateSut() => new(_repo.Object, _projects.Object);

    [Fact]
    public async Task Handle_ArchivesDepartment_SoftDelete_WhenNoActiveProject()
    {
        _projects.Setup(p => p.HasActiveByDepartmentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.ArchiveAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await CreateSut().Handle(new DepartmentDeleteCommand(5), CancellationToken.None);

        result.Should().BeTrue();
        _repo.Verify(r => r.ArchiveAsync(5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenDepartmentNotFound()
    {
        _projects.Setup(p => p.HasActiveByDepartmentAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(r => r.ArchiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await CreateSut().Handle(new DepartmentDeleteCommand(404), CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Throws409_WhenHasActiveProject()
    {
        // Lưu ý #5: còn đơn đang sản xuất → không được giải tán xưởng.
        _projects.Setup(p => p.HasActiveByDepartmentAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => CreateSut().Handle(new DepartmentDeleteCommand(5), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _repo.Verify(r => r.ArchiveAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
