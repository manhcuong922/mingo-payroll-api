using FluentAssertions;
using Moq;
using MPS.Application.Features.Role.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;
using Entity = MPS.Domain.Entities.Role;

namespace MPS.Application.Tests.Features.Role;

public sealed class RoleUpdateCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _repo = new();

    private RoleUpdateCommandHandler CreateSut() => new(_repo.Object);

    [Fact]
    public async Task Handle_ReturnsFalse_WhenRoleNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var ok = await CreateSut().Handle(new RoleUpdateCommand(99, "X", null), CancellationToken.None);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Throws_WhenRoleIsSystem()
    {
        _repo.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity { Id = 1, Name = "SystemAdmin", IsSystem = true });

        var act = () => CreateSut().Handle(new RoleUpdateCommand(1, "Renamed", null), CancellationToken.None);

        await act.Should().ThrowAsync<SystemRoleModificationException>();
    }

    [Fact]
    public async Task Handle_UpdatesCustomRole_OnSuccess()
    {
        var existing = new Entity { Id = 8, Name = "Old", IsSystem = false };
        _repo.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<Entity>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await CreateSut().Handle(new RoleUpdateCommand(8, "New", "Desc"), CancellationToken.None);

        ok.Should().BeTrue();
        existing.Name.Should().Be("New");
        existing.Description.Should().Be("Desc");
    }
}
