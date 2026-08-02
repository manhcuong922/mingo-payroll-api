using FluentAssertions;
using Microsoft.Extensions.Options;
using MPS.Domain.Entities;
using MPS.Infrastructure.Security;

namespace MPS.Infrastructure.Tests.Security;

/// <summary>
/// C1 — Một tài khoản có "chìa khoá vạn năng" 10 năm, không thu hồi được.
///
/// <para><see cref="JwtTokenService.GenerateAccessToken"/> đặc cách phiên 10 năm cho tài khoản
/// có <c>Username == "ai-service"</c>. Hệ quả: (1) giảm quyền vai trò hay khoá tài khoản đều không
/// tác động tới phiên đã cấp; (2) chuỗi phiên lộ ra thì cách duy nhất để thu hồi là đổi khoá ký
/// toàn hệ thống; (3) đặc quyền gắn vào TÊN ĐĂNG NHẬP — mà tên đăng nhập là trường sửa được
/// (<c>UserUpdateCommand.Username</c>) ⇒ ai sửa được người dùng thì tự cấp cho mình phiên 10 năm.</para>
/// </summary>
public sealed class JwtLongLivedTokenTests
{
    private const string PrivilegedUsername = "ai-service";

    private static JwtSettings Settings() => new()
    {
        Issuer = "test-issuer",
        Audience = "test-aud",
        SecretKey = "TEST_SECRET_KEY_AT_LEAST_32_BYTES_LONG!!!!!!",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 1,
        ClockSkewSeconds = 0,
    };

    private static User UserNamed(string username, int roleId = 7) => new()
    {
        Id = 99,
        Username = username,
        Email = $"{username}@test",
        RoleId = roleId,
        Role = new Role { Id = roleId, Name = "Employee", IsSystem = true, CreatedAt = DateTime.UtcNow },
    };

    [Fact(DisplayName = "C1 · Không tài khoản nào được cấp phiên dài hơn cấu hình")]
    public void C1_NoAccount_MayReceiveALongerSessionThanConfigured()
    {
        var settings = Settings();
        var sut = new JwtTokenService(Options.Create(settings));

        var result = sut.GenerateAccessToken(UserNamed(PrivilegedUsername), Array.Empty<string>());

        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(settings.AccessTokenMinutes), TimeSpan.FromMinutes(1),
            "phiên 10 năm không thu hồi được bằng bất kỳ thao tác quản trị nào (giảm quyền, khoá tài " +
            "khoản); tài khoản kỹ thuật phải dùng refresh token như mọi tài khoản khác");
    }

    [Fact(DisplayName = "C1 · Đặc quyền phiên không được gắn vào tên đăng nhập (trường sửa được)")]
    public void C1_Privilege_MustNotBeKeyedOnMutableUsername()
    {
        var sut = new JwtTokenService(Options.Create(Settings()));

        // Kẻ tấn công có quyền `users.update` đổi username của mình thành "ai-service".
        var impersonator = sut.GenerateAccessToken(UserNamed(PrivilegedUsername, roleId: 7), Array.Empty<string>());
        var ordinary = sut.GenerateAccessToken(UserNamed("nguyenvana", roleId: 7), Array.Empty<string>());

        impersonator.ExpiresAt.Should().BeCloseTo(ordinary.ExpiresAt, TimeSpan.FromMinutes(1),
            "hai người dùng CÙNG vai trò mà chỉ khác tên đăng nhập lại nhận vòng đời phiên khác nhau " +
            "⇒ đổi tên tài khoản là một đường leo thang đặc quyền");
    }

    [Fact(DisplayName = "C1 · Đối chứng — tài khoản thường nhận đúng AccessTokenMinutes")]
    public void C1_Control_OrdinaryAccount_GetsConfiguredLifetime()
    {
        var sut = new JwtTokenService(Options.Create(Settings()));

        var result = sut.GenerateAccessToken(UserNamed("nguyenvana"), Array.Empty<string>());

        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1),
            "test đối chứng: cấu hình vòng đời phiên hoạt động đúng cho mọi tài khoản khác");
    }
}
