using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;

namespace MPS.Infrastructure.Email;

/// Dev/test email sender that logs the message instead of sending.
/// Swap with SmtpEmailSender / SendGridEmailSender in production via DI.
public sealed class LoggingEmailSender : IRealEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string resetToken,
        string resetUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL] Password reset for {Email}. Url: {Url}. Token: {Token}. Expires at: {ExpiresAt:u}.",
            toEmail, resetUrl, resetToken, expiresAt);
        return Task.CompletedTask;
    }

    [Obsolete("Dùng SendFirstLoginInvitationAsync cho tài khoản mới. Xem plan §2 N8.")]
    public Task SendAccountCredentialsAsync(
        string toEmail,
        string fullName,
        string username,
        string plainPassword,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL] Account credentials for {FullName} <{Email}>. Username: {Username}. Password: {Password}.",
            fullName, toEmail, username, plainPassword);
        return Task.CompletedTask;
    }

    public Task SendFirstLoginInvitationAsync(
        string toEmail,
        string fullName,
        string username,
        string invitationUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL] First-login invitation for {FullName} <{Email}>. Username: {Username}. Url: {Url}. Expires: {Expires:u}.",
            fullName, toEmail, username, invitationUrl, expiresAt);
        return Task.CompletedTask;
    }

    public Task SendContractActivationAsync(
        string toEmail,
        string fullName,
        ContractActivationEmailPayload payload,
        string storageBucket,
        string storageObjectKey,
        string pdfFileName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL] Contract activation for {FullName} <{Email}>. Contract={ContractNo} From={From:d} To={To:d} " +
            "PdfFile={PdfFile} PdfKey={Bucket}/{ObjectKey}.",
            fullName, toEmail, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo,
            pdfFileName, storageBucket, storageObjectKey);
        return Task.CompletedTask;
    }

    public Task SendGenericNotificationAsync(
        string toEmail, string subject, string textBody, string? htmlBody,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL] Notification to {Email} subject={Subject} bodyPreview={Preview}",
            toEmail, subject, textBody.Length <= 200 ? textBody : textBody[..200] + "...");
        return Task.CompletedTask;
    }
}
