using AutoMapper;
using FluentAssertions;
using Moq;
using MPS.Application.Common.Interfaces;
using MPS.Application.DTOs;
using MPS.Application.Features.User.Queries;
using MPS.Application.Tests.TestHelpers;
using MPS.Domain.Constants;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;
using Xunit;

namespace MPS.Application.Tests.Features.User;

public sealed class UserGetByIdQueryHandlerTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private UserGetByIdQueryHandler CreateSut() =>
        new(_users.Object, _mapper.Object, _currentUser.Object);

    [Fact]
    public async Task Handle_AllowsAnyUser_WhenHasReadAll()
    {
        var user = UserFactory.CreateActive(id: 42);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserReadAll)).Returns(true);
        _users.Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mapper.Setup(m => m.Map<UserDto>(user))
            .Returns(new UserDto(42, "EC", "F", 7, "Employee", "u", "e", DateTime.UtcNow, "p", 1, null));

        var result = await CreateSut().Handle(new UserGetByIdQuery(42), CancellationToken.None);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ThrowsForbidden_WhenSelfOnlyAndRequestingOther()
    {
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserReadAll)).Returns(false);

        var act = () => CreateSut().Handle(new UserGetByIdQuery(42), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("You can only view your own user info.");
    }

    [Fact]
    public async Task Handle_AllowsSelf_WhenSelfOnlyAndIdMatches()
    {
        var user = UserFactory.CreateActive(id: 99);
        _currentUser.SetupGet(c => c.UserId).Returns(99);
        _currentUser.Setup(c => c.HasPermission(Permissions.UserReadAll)).Returns(false);
        _users.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        _mapper.Setup(m => m.Map<UserDto>(user))
            .Returns(new UserDto(99, "EC", "F", 7, "Employee", "u", "e", DateTime.UtcNow, "p", 1, null));

        var result = await CreateSut().Handle(new UserGetByIdQuery(99), CancellationToken.None);

        result.Should().NotBeNull();
    }
}
