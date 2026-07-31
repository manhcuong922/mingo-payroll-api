using FluentAssertions;
using MPS.Application.Features.Notifications.Recipients;
using Xunit;

namespace MPS.Application.Tests.Features.Notifications;

/// <summary>
/// Kiểm tra <see cref="RecipientSpec"/>: immutable + fluent + gộp không trùng lặp.
/// </summary>
public sealed class RecipientSpecTests
{
    [Fact]
    public void None_IsEmpty()
    {
        RecipientSpec.None.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void FluentToPermissions_MergesWithoutDuplicates()
    {
        var spec = RecipientSpec.Permissions("A", "B").ToPermissions(new[] { "B", "C" });

        spec.PermissionCodes.Should().BeEquivalentTo(new[] { "A", "B", "C" });
    }

    [Fact]
    public void FluentIsImmutable_OriginalUnchanged_AfterExcluding()
    {
        var original = RecipientSpec.Users(1, 2, 3);
        var withoutTwo = original.Excluding(2);

        original.ExcludeUserIds.Should().BeEmpty("original phải giữ nguyên (immutable)");
        withoutTwo.ExcludeUserIds.Should().BeEquivalentTo(new[] { 2 });
        original.UserIds.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void Everyone_IsBroadcastNotEmpty()
    {
        var spec = RecipientSpec.Everyone();

        spec.AllUsers.Should().BeTrue();
        spec.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void PermissionsAlone_NotEmpty()
    {
        var spec = RecipientSpec.Permissions("Payroll.Read.All");

        spec.IsEmpty.Should().BeFalse();
        spec.PermissionCodes.Should().ContainSingle().Which.Should().Be("Payroll.Read.All");
    }

    [Fact]
    public void ExcludingNull_IsIgnored()
    {
        var spec = RecipientSpec.Users(1).Excluding((int?)null);

        spec.ExcludeUserIds.Should().BeEmpty();
    }
}
