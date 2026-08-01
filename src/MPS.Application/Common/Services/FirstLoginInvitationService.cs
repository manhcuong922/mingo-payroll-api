using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Domain.Entities;
using MPS.Domain.Exceptions;
using MPS.Domain.Interfaces;

namespace MPS.Application.Common.Services;

/// <summary>
/// Xem <see cref="IFirstLoginInvitationService"/>. Cơ chế token giống hệt <c>ForgotPasswordCommandHandler</c>
/// (SHA-256 hash lưu DB, raw token gửi mail) — chỉ khác thời hạn (invitation dài hơn 72h vs 1h)
/// và mail template ("Thiết lập mật khẩu" vs "Đặt lại mật khẩu").
/// </summary>
public sealed class FirstLoginInvitationService : IFirstLoginInvitationService
{
    private const int RawTokenBytes = 48;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailSender _emailSender;
    private readonly FirstLoginOptions _options;

    public FirstLoginInvitationService(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IEmailSender emailSender,
        IOptions<FirstLoginOptions> options)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailSender = emailSender;
        _options = options.Value;
    }

    public async Task<DateTime> IssueAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException($"User {userId} not found.");

        if (string.IsNullOrWhiteSpace(user.Email))
            throw new DomainValidationException(
                $"USER_EMAIL_MISSING: User {userId} has no email — cannot send first-login invitation.");

        await _tokenRepository.InvalidateActiveTokensForUserAsync(user.Id, cancellationToken);

        var rawToken = GenerateRawToken();
        var expiresAt = DateTime.UtcNow.Add(_options.TokenLifetime);

        await _tokenRepository.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            CreatedByIp = null,
        }, cancellationToken);

        var invitationUrl = BuildInvitationUrl(rawToken);

        await _emailSender.SendFirstLoginInvitationAsync(
            toEmail: user.Email,
            fullName: user.FullName,
            username: user.Username,
            invitationUrl: invitationUrl,
            expiresAt: expiresAt,
            cancellationToken);

        return expiresAt;
    }

    private string BuildInvitationUrl(string rawToken)
    {
        var baseUrl = _options.FrontendBaseUrl?.TrimEnd('/');
        var path = _options.LinkPath.Replace("{token}", Uri.EscapeDataString(rawToken));
        if (!path.StartsWith('/')) path = "/" + path;

        // Nếu chưa cấu hình FrontendBaseUrl, vẫn phải trả về gì đó có ý nghĩa để user thao tác:
        // chỉ trả về path — HR/dev nhìn log/UI sẽ biết cấu hình thiếu và tự bổ sung domain.
        return string.IsNullOrWhiteSpace(baseUrl) ? path : baseUrl + path;
    }

    private static string GenerateRawToken()
    {
        Span<byte> buffer = stackalloc byte[RawTokenBytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string raw)
    {
        var bytes = Encoding.UTF8.GetBytes(raw);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
