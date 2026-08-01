using FluentAssertions;
using Moq;
using MPS.Application.Features.Notifications.Recipients;
using Xunit;

namespace MPS.Application.Tests.Features.Notifications;

/// <summary>
/// Kiểm tra logic thuần của <see cref="RecipientResolver"/>: gộp Users/Roles/Departments/Permissions
/// qua danh bạ, distinct rồi trừ Exclude. Directory mock, không đụng DB.
/// </summary>
public sealed class RecipientResolverTests
{
    private readonly Mock<INotificationRecipientDirectory> _directory = new();

    private RecipientResolver CreateSut() => new(_directory.Object);

    [Fact]
    public async Task ResolveAsync_ReturnsEmpty_WhenSpecIsNone()
    {
        var result = await CreateSut().ResolveAsync(RecipientSpec.None, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_UnionsUsersRolesDepartmentsPermissions_Distinct()
    {
        _directory.Setup(d => d.GetUserIdsByRolesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10, 20 });
        _directory.Setup(d => d.GetUserIdsByDepartmentsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 20, 30 });
        _directory.Setup(d => d.GetUserIdsByPermissionsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 30, 40 });

        var spec = RecipientSpec.Users(1, 2)
            .ToRoles(new[] { 100 })
            .ToDepartments(new[] { 5 })
            .ToPermissions(new[] { "Payroll.Read.All" });

        var result = await CreateSut().ResolveAsync(spec, CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { 1, 2, 10, 20, 30, 40 });
    }

    [Fact]
    public async Task ResolveAsync_RemovesExcludedUsers()
    {
        _directory.Setup(d => d.GetUserIdsByPermissionsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 10, 20, 30 });

        var spec = RecipientSpec.Permissions("X").Excluding(20);

        var result = await CreateSut().ResolveAsync(spec, CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { 10, 30 });
    }

    [Fact]
    public async Task ResolveAsync_Everyone_UsesAllUsersEndpoint_IgnoresOtherSets()
    {
        _directory.Setup(d => d.GetAllActiveUserIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 3 });

        var result = await CreateSut().ResolveAsync(RecipientSpec.Everyone(), CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        _directory.Verify(d => d.GetUserIdsByRolesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _directory.Verify(d => d.GetUserIdsByPermissionsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_DoesNotCallDirectory_WhenSpecOnlyHasExplicitUserIds()
    {
        var spec = RecipientSpec.Users(1, 2, 3);

        var result = await CreateSut().ResolveAsync(spec, CancellationToken.None);

        result.Should().BeEquivalentTo(new[] { 1, 2, 3 });
        _directory.Verify(d => d.GetUserIdsByRolesAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _directory.Verify(d => d.GetUserIdsByDepartmentsAsync(It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
        _directory.Verify(d => d.GetUserIdsByPermissionsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
