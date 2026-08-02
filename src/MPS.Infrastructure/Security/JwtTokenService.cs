using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Models;
using MPS.Domain.Entities;

namespace MPS.Infrastructure.Security;

public sealed class JwtTokenService : IJwtTokenService
{
    public const string PermissionsClaimType = "perms";
    public const string RoleIdClaimType = "role_id";

    private readonly JwtSettings _settings;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;

        if (string.IsNullOrWhiteSpace(_settings.SecretKey))
            throw new InvalidOperationException("Jwt:SecretKey is not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(_settings.SecretKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes (256 bits) for HS256.");

        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(keyBytes),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessTokenResult GenerateAccessToken(User user, IEnumerable<string> permissionCodes)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = DateTime.UtcNow;

        // ⭐ C1 — MỌI tài khoản dùng chung vòng đời phiên đã cấu hình. Không đặc cách cho ai.
        //
        // Trước đây tài khoản có `Username == "ai-service"` được cấp phiên **10 năm**. Ba hệ quả:
        //   • giảm quyền vai trò ⇒ vô tác dụng, phiên cũ vẫn mang nguyên bộ quyền đã đóng vào token;
        //   • khoá tài khoản ⇒ phiên cũ vẫn dùng được;
        //   • chuỗi token lộ ra (log, ảnh chụp màn hình, repo) ⇒ cách duy nhất để thu hồi là đổi
        //     khoá ký của cả hệ thống, tức bắt TẤT CẢ mọi người đăng nhập lại.
        //
        // Nặng nhất: đặc quyền gắn vào **tên đăng nhập**, mà tên đăng nhập là trường SỬA ĐƯỢC
        // (`UserUpdateCommand.Username`) ⇒ bất kỳ ai có quyền sửa người dùng chỉ cần đổi tên tài
        // khoản của mình thành "ai-service" là tự cấp cho mình một phiên 10 năm.
        //
        // Tài khoản kỹ thuật dùng chung luồng refresh token như mọi tài khoản khác — nhờ đó thu hồi
        // được bằng cách vô hiệu refresh token, không phải đổi khoá ký. Nếu về sau thật sự cần một
        // credential sống lâu cho máy-với-máy thì phải là API key có danh sách thu hồi riêng, KHÔNG
        // phải JWT tự chứng (không thu hồi được trước hạn).
        var expiresAt = issuedAt.AddMinutes(_settings.AccessTokenMinutes);
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email),
            new(RoleIdClaimType, user.RoleId.ToString()),
        };

        if (user.Role is not null)
            claims.Add(new Claim(ClaimTypes.Role, user.Role.Name));

        foreach (var perm in permissionCodes.Distinct(StringComparer.Ordinal))
            claims.Add(new Claim(PermissionsClaimType, perm));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: _signingCredentials);

        var serialized = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessTokenResult(serialized, expiresAt, jti);
    }
}
