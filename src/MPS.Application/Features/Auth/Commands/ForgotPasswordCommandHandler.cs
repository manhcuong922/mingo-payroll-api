using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Application.Common.Options;
using MPS.Application.Common.Services;
using MPS.Domain.Entities;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.Auth.Commands;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, bool>
{
    private const int RawTokenBytes = 48;

    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _tokenRepository;
    private readonly IEmailSender _emailSender;
    private readonly PasswordResetOptions _options;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository tokenRepository,
        IEmailSender emailSender,
        IOptions<PasswordResetOptions> options,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tokenRepository = tokenRepository;
        _emailSender = emailSender;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        // Always return true (don't leak whether email exists).
        var users = await _userRepository.GetAllAsync(cancellationToken);
        var user = users.FirstOrDefault(u => string.Equals(u.Email, request.Email, StringComparison.OrdinalIgnoreCase));

        if (user is null || user.Status != 1)
        {
            _logger.LogInformation("Password reset requested for non-existent/disabled email {Email}.", request.Email);
            return true;
        }

        await _tokenRepository.InvalidateActiveTokensForUserAsync(user.Id, cancellationToken);

        var rawToken = GenerateRawToken();
        var expiresAt = DateTime.UtcNow.Add(_options.TokenLifetime);

        var entity = new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = HashToken(rawToken),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            CreatedByIp = request.ClientIp,
        };

        await _tokenRepository.AddAsync(entity, cancellationToken);

        // Mail phải chứa link bấm được (không bắt user tự ghép token vào URL) — cùng cách làm
        // với mail mời đặt mật khẩu lần đầu. Token thô vẫn gửi kèm để template fallback khi
        // FrontendBaseUrl chưa cấu hình.
        var resetUrl = FrontendLinkBuilder.Build(_options.FrontendBaseUrl, _options.LinkPath, rawToken);

        await _emailSender.SendPasswordResetAsync(user.Email, rawToken, resetUrl, expiresAt, cancellationToken);
        return true;
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
