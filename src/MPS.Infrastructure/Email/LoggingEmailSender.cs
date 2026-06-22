using Microsoft.Extensions.Logging;
using MPS.Application.Common.Interfaces;

namespace MPS.Infrastructure.Email;

/// Dev/test email sender that logs the message instead of sending.
/// Swap with SmtpEmailSender / SendGridEmailSender in production via DI.
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string resetToken,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL] Password reset for {Email}. Token: {Token}. Expires at: {ExpiresAt:u}.",
            toEmail, resetToken, expiresAt);
        return Task.CompletedTask;
    }

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

    public Task SendContractActivationAsync(
        string toEmail,
        string fullName,
        ContractActivationEmailPayload payload,
        Stream pdfStream,
        string pdfFileName,
        CancellationToken cancellationToken = default)
    {
        var attachmentSize = pdfStream.CanSeek ? pdfStream.Length : -1L;
        if (payload.Username is not null)
        {
            _logger.LogWarning(
                "[DEV EMAIL] Contract activation for {FullName} <{Email}>. Contract={ContractNo} From={From:d} To={To:d} " +
                "Username={Username} Password={Password} PdfFile={PdfFile} PdfSize={PdfSize}.",
                fullName, toEmail, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo,
                payload.Username, payload.PlainPassword, pdfFileName, attachmentSize);
        }
        else
        {
            _logger.LogWarning(
                "[DEV EMAIL] Contract activation for {FullName} <{Email}>. Contract={ContractNo} From={From:d} To={To:d} " +
                "(no credentials, account already exists). PdfFile={PdfFile} PdfSize={PdfSize}.",
                fullName, toEmail, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo,
                pdfFileName, attachmentSize);
        }
        return Task.CompletedTask;
    }
}
