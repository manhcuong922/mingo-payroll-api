using FluentAssertions;
using MPS.Application.Common.Exceptions.Storage;
using MPS.Application.Common.Storage;

namespace MPS.Infrastructure.Tests.Storage;

public sealed class ObjectKeyBuilderTests
{
    [Fact]
    public void ForContract_BuildsKey_WithYearMonthAndVersion()
    {
        var key = ObjectKeyBuilder.ForContract("HD-2026-001", version: 2);

        key.Should().StartWith("contracts/");
        key.Should().EndWith("/HD-2026-001/v2.pdf");
        key.Should().MatchRegex(@"^contracts/\d{4}/\d{2}/HD-2026-001/v2\.pdf$");
    }

    [Fact]
    public void ForAppendix_BuildsKey_WithAppendixNoAndVersion()
    {
        var key = ObjectKeyBuilder.ForAppendix("HD-2026-001", "A01", version: 1);

        key.Should().MatchRegex(@"^appendices/\d{4}/\d{2}/HD-2026-001/A01-v1\.pdf$");
    }

    [Fact]
    public void ForTerminationNotice_BuildsKey_WithVersion()
    {
        var key = ObjectKeyBuilder.ForTerminationNotice("HD-2026-001", version: 1);

        key.Should().MatchRegex(@"^terminations/\d{4}/\d{2}/HD-2026-001/notice-v1\.pdf$");
    }

    [Fact]
    public void ForAuthorization_BuildsKey_WithAuthIdAndVersion()
    {
        var authId = Guid.NewGuid();

        var key = ObjectKeyBuilder.ForAuthorization(authId, version: 1);

        key.Should().Contain(authId.ToString("N"));
        key.Should().EndWith("/v1.pdf");
    }

    [Fact]
    public void ForPayslip_BuildsDeterministicKey_PerEmployeeAndMonth()
    {
        var key = ObjectKeyBuilder.ForPayslip(employeeId: 42, year: 2026, month: 6);

        key.Should().Be("payslips/2026/06/42.pdf");
    }

    [Fact]
    public void ForPayslip_Throws_WhenMonthOutOfRange()
    {
        var act = () => ObjectKeyBuilder.ForPayslip(1, 2026, 13);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ForEmployeeAvatar_NormalisesExtension()
    {
        var key = ObjectKeyBuilder.ForEmployeeAvatar(7, ".PNG");

        key.Should().Be("avatars/7.png");
    }

    [Fact]
    public void Build_Rejects_DotDotInRefKey()
    {
        var act = () => ObjectKeyBuilder.ForContract("../etc/passwd", 1);

        act.Should().Throw<InvalidObjectKeyException>();
    }

    [Fact]
    public void Build_Rejects_SlashInRefKey()
    {
        var act = () => ObjectKeyBuilder.ForContract("a/b", 1);

        act.Should().Throw<InvalidObjectKeyException>();
    }

    [Fact]
    public void Build_Rejects_BackslashInRefKey()
    {
        var act = () => ObjectKeyBuilder.ForContract("a\\b", 1);

        act.Should().Throw<InvalidObjectKeyException>();
    }

    [Fact]
    public void Build_Rejects_NullByte()
    {
        var act = () => ObjectKeyBuilder.ForContract("a\0b", 1);

        act.Should().Throw<InvalidObjectKeyException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Build_Rejects_EmptyRefKey(string refKey)
    {
        var act = () => ObjectKeyBuilder.ForContract(refKey, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EnsureValid_Passes_OnNormalKey()
    {
        var act = () => ObjectKeyBuilder.EnsureValid("contracts/2026/06/HD-001/v1.pdf");

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    [InlineData("/leading")]
    [InlineData("\\windows\\path")]
    [InlineData("contains/../traversal")]
    [InlineData("null\0byte")]
    public void EnsureValid_Throws_OnInvalidKey(string key)
    {
        var act = () => ObjectKeyBuilder.EnsureValid(key);

        act.Should().Throw<InvalidObjectKeyException>();
    }

    [Fact]
    public void EnsureValid_Throws_WhenKeyTooLong()
    {
        var longKey = new string('a', 1025);

        var act = () => ObjectKeyBuilder.EnsureValid(longKey);

        act.Should().Throw<InvalidObjectKeyException>();
    }
}
