using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.Role.Commands;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;
using PermissionEntity = MPS.Domain.Entities.Permission;
using RoleEntity = MPS.Domain.Entities.Role;

namespace MPS.Application.Tests.Features.Role;

public sealed class UpdateRolePermissionsCommandHandlerTests
{
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IRolePermissionRepository> _rolePerms = new();
    private readonly Mock<IPermissionRepository> _perms = new();
    private readonly Mock<IPermissionResolver> _resolver = new();

    private static readonly int[] SystemAdminRequired =
    {
        PermissionIds.RoleReadAll,
        PermissionIds.RoleCreateAll,
        PermissionIds.RoleUpdateAll,
        PermissionIds.RoleDeleteAll,
        PermissionIds.RoleManagePermissionsAll,
        PermissionIds.RefreshTokenRevokeAll,
    };

    private UpdateRolePermissionsCommandHandler CreateSut() =>
        new(_roles.Object, _rolePerms.Object, _perms.Object, _resolver.Object);

    [Fact]
    public async Task Handle_ReturnsFalse_WhenRoleNotFound()
    {
        _roles.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((RoleEntity?)null);

        var ok = await CreateSut().Handle(new UpdateRolePermissionsCommand(99, Array.Empty<int>()), CancellationToken.None);

        ok.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_Throws_WhenPermissionIdInvalid()
    {
        const int customRoleId = 8;
        _roles.Setup(r => r.GetByIdAsync(customRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleEntity { Id = customRoleId, IsSystem = false });
        _perms.Setup(p => p.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionEntity> { new() { Id = 1, Code = "X" } });

        var act = () => CreateSut().Handle(new UpdateRolePermissionsCommand(customRoleId, new[] { 1, 999 }), CancellationToken.None);

        await act.Should().ThrowAsync<DomainValidationException>();
    }

    [Fact]
    public async Task Handle_UpdatesCustomRole_AndInvalidatesCache()
    {
        const int customRoleId = 8;
        _roles.Setup(r => r.GetByIdAsync(customRoleId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleEntity { Id = customRoleId, IsSystem = false });
        _perms.Setup(p => p.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PermissionEntity> { new() { Id = 1 }, new() { Id = 2 } });

        var ok = await CreateSut().Handle(new UpdateRolePermissionsCommand(customRoleId, new[] { 1, 2 }), CancellationToken.None);

        ok.Should().BeTrue();
        _rolePerms.Verify(p => p.ReplaceRolePermissionsAsync(
            customRoleId,
            It.Is<IEnumerable<int>>(ids => ids.OrderBy(x => x).SequenceEqual(new[] { 1, 2 })),
            It.IsAny<CancellationToken>()), Times.Once);
        _resolver.Verify(r => r.InvalidateRoleCache(customRoleId), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsSystemRoleModification_WhenSystemAdminMissingCriticalPerms()
    {
        _roles.Setup(r => r.GetByIdAsync(SystemRoles.SystemAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleEntity { Id = SystemRoles.SystemAdminId, IsSystem = true });

        var act = () => CreateSut().Handle(
            new UpdateRolePermissionsCommand(SystemRoles.SystemAdminId, new[] { 1, 2 }),
            CancellationToken.None);

        await act.Should().ThrowAsync<SystemRoleModificationException>()
            .WithMessage("*SystemAdmin must retain*");
    }

    [Fact]
    public async Task Handle_RejectsEmptyPermissionSet_OnSystemAdmin()
    {
        _roles.Setup(r => r.GetByIdAsync(SystemRoles.SystemAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleEntity { Id = SystemRoles.SystemAdminId, IsSystem = true });

        var act = () => CreateSut().Handle(
            new UpdateRolePermissionsCommand(SystemRoles.SystemAdminId, Array.Empty<int>()),
            CancellationToken.None);

        await act.Should().ThrowAsync<SystemRoleModificationException>();
    }

    [Fact]
    public async Task Handle_AllowsSystemAdmin_WhenCriticalPermsRetained()
    {
        _roles.Setup(r => r.GetByIdAsync(SystemRoles.SystemAdminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoleEntity { Id = SystemRoles.SystemAdminId, IsSystem = true });
        _perms.Setup(p => p.GetByIdsAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemAdminRequired.Select(id => new PermissionEntity { Id = id }).ToList());

        var ok = await CreateSut().Handle(
            new UpdateRolePermissionsCommand(SystemRoles.SystemAdminId, SystemAdminRequired),
            CancellationToken.None);

        ok.Should().BeTrue();
        _resolver.Verify(r => r.InvalidateRoleCache(SystemRoles.SystemAdminId), Times.Once);
    }
}
