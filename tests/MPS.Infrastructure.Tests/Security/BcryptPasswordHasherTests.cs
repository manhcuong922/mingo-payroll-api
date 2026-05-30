using FluentAssertions;
using MPS.Infrastructure.Security;
using Xunit;

namespace MPS.Infrastructure.Tests.Security;

public sealed class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _sut = new();

    [Fact]
    public void Hash_ProducesNonEmptyHash()
    {
        var hash = _sut.Hash("MyPassword!");

        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().NotBe("MyPassword!");
    }

    [Fact]
    public void Hash_ProducesDifferentHashesForSameInput()
    {
        var a = _sut.Hash("MyPassword!");
        var b = _sut.Hash("MyPassword!");

        a.Should().NotBe(b, "BCrypt salt should produce different hashes each time");
    }

    [Fact]
    public void Verify_ReturnsTrue_ForCorrectPassword()
    {
        var hash = _sut.Hash("MyPassword!");
        _sut.Verify("MyPassword!", hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForWrongPassword()
    {
        var hash = _sut.Hash("MyPassword!");
        _sut.Verify("Different", hash).Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForEmptyInputs()
    {
        _sut.Verify("", "anything").Should().BeFalse();
        _sut.Verify("pw", "").Should().BeFalse();
    }

    [Fact]
    public void Verify_ReturnsFalse_ForMalformedHash()
    {
        _sut.Verify("pw", "not-a-bcrypt-hash").Should().BeFalse();
    }

    [Fact]
    public void Hash_ThrowsForEmptyPassword()
    {
        var act = () => _sut.Hash("");
        act.Should().Throw<ArgumentException>();
    }
}
