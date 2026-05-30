using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.Features.User.Commands;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.User;

public sealed class UserUpdateCommandHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IRefreshTokenService> _refresh = new();

    private UserUpdateCommandHandler CreateSut() =>
        new(_users.Object, _hasher.Object, _currentUser.Object, _refresh.Object);

    private UserUpdateCommand CommandFor(int id, int roleId = 7, int status = 1) => new(
        Id: id, EmployeeCode: "EC", FullName: "F", RoleId: roleId, Username: "u",
        Password: null, Email: "e@x", Dob: DateTime.UtcNow, PhoneNo: "0", Status: status);

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenSelfTriesUpdatingOther()
    {
        var existing = UserFactory.CreateActive(id: 42);
        _users.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserUpdateAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);

        var act = () => CreateSut().Handle(CommandFor(42), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You can only update your own user info.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenSelfChangesOwnRole()
    {
        var existing = UserFactory.CreateActive(id: 99, roleId: 7);
        _users.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserUpdateAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);

        var act = () => CreateSut().Handle(CommandFor(99, roleId: 1), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You cannot change your own role.");
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenSelfChangesOwnStatus()
    {
        var existing = UserFactory.CreateActive(id: 99, status: 1);
        _users.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserUpdateAll)).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns(99);

        var act = () => CreateSut().Handle(CommandFor(99, roleId: 7, status: 0), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You cannot change your own status.");
    }

    [Fact]
    public async Task Handle_RevokesTokens_WhenAdminDisablesUser()
    {
        var existing = UserFactory.CreateActive(id: 42, status: 1);
        _users.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _users.Setup(r => r.UpdateAsync(It.IsAny<MPS.Domain.Entities.User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserUpdateAll)).Returns(true);

        var ok = await CreateSut().Handle(CommandFor(42, status: 0), CancellationToken.None);

        ok.Should().BeTrue();
        _refresh.Verify(r => r.RevokeAllForUserAsync(42, "User disabled by admin", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RevokesTokens_WhenAdminChangesRole()
    {
        var existing = UserFactory.CreateActive(id: 42, roleId: 7);
        _users.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _users.Setup(r => r.UpdateAsync(It.IsAny<MPS.Domain.Entities.User>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserUpdateAll)).Returns(true);

        var ok = await CreateSut().Handle(CommandFor(42, roleId: 5), CancellationToken.None);

        ok.Should().BeTrue();
        _refresh.Verify(r => r.RevokeAllForUserAsync(42, "User role changed", null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
