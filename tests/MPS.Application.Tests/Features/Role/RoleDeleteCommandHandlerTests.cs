using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Role.Commands;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;
using Entity = MPS.Domain.Entities.Role;

namespace MPS.Application.Tests.Features.Role;

public sealed class RoleDeleteCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IRolePermissionRepository> _rolePerms = new();
    private readonly Mock<IPermissionResolver> _resolver = new();

    private RoleDeleteCommandHandler CreateSut() =>
        new(_roles.Object, _rolePerms.Object, _resolver.Object);

    [Fact]
    public async Task Handle_ReturnsFalse_WhenRoleNotFound()
    {
        _roles.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Entity?)null);

        var ok = await CreateSut().Handle(new RoleDeleteCommand(99), CancellationToken.None);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Throws_WhenSystemRole()
    {
        _roles.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity { Id = 1, IsSystem = true });

        var act = () => CreateSut().Handle(new RoleDeleteCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<SystemRoleModificationException>();
    }

    [Fact]
    public async Task Handle_Throws_WhenRoleAssignedToUsers()
    {
        _roles.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity { Id = 8, IsSystem = false });
        _roles.Setup(r => r.HasUsersAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => CreateSut().Handle(new RoleDeleteCommand(8), CancellationToken.None);

        await act.Should().ThrowAsync<RoleInUseException>();
    }

    [Fact]
    public async Task Handle_DeletesRole_AndInvalidatesCache_OnSuccess()
    {
        _roles.Setup(r => r.GetByIdAsync(8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entity { Id = 8, IsSystem = false });
        _roles.Setup(r => r.HasUsersAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _roles.Setup(r => r.DeleteAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ok = await CreateSut().Handle(new RoleDeleteCommand(8), CancellationToken.None);

        ok.Should().BeTrue();
        _rolePerms.Verify(p => p.DeleteByRoleIdAsync(8, It.IsAny<CancellationToken>()), Times.Once);
        _resolver.Verify(r => r.InvalidateRoleCache(8), Times.Once);
    }
}
