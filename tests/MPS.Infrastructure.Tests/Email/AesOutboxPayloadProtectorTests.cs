using System.Security.Cryptography;
using FluentAssertions;
using MPS.Infrastructure.Email;

namespace MPS.Infrastructure.Tests.Email;

public sealed class AesOutboxPayloadProtectorTests
{
    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    [Fact]
    public void ProtectThenUnprotect_RoundTrips()
    {
        var protector = new AesOutboxPayloadProtector(NewKey());
        const string plain = "{\"ResetToken\":\"abc-123\",\"ExpiresAt\":\"2026-07-01T09:00:00Z\"}";

        var cipher = protector.Protect(plain);
        var back = protector.Unprotect(cipher);

        cipher.Should().NotBe(plain);
        cipher.Should().NotContain("abc-123");
        back.Should().Be(plain);
    }

    [Fact]
    public void Protect_ProducesDifferentCiphertext_EachCall_DueToRandomNonce()
    {
        var protector = new AesOutboxPayloadProtector(NewKey());

        var a = protector.Protect("same-input");
        var b = protector.Protect("same-input");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Unprotect_WithWrongKey_Throws()
    {
        var cipher = new AesOutboxPayloadProtector(NewKey()).Protect("secret");
        var other = new AesOutboxPayloadProtector(NewKey());

        var act = () => other.Unprotect(cipher);

        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("not-base64!!!")]
    [InlineData("aGVsbG8=")] // "hello" = 5 bytes, không đủ 32
    public void Constructor_WithInvalidKey_ThrowsInvalidOperation(string badKey)
    {
        var act = () => new AesOutboxPayloadProtector(badKey);

        act.Should().Throw<InvalidOperationException>();
    }
}
