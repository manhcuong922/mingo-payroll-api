using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MPS.Application.Common.Interfaces;
using MPS.Infrastructure.Email.Templates;

namespace MPS.Infrastructure.Email;

public sealed class SmtpEmailSender : IRealEmailSender
{
    private readonly SmtpOptions _options;
    private readonly IFileStorageService _storage;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<SmtpOptions> options,
        IFileStorageService storage,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _storage = storage;
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string toEmail,
        string resetToken,
        string resetUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var html = PasswordResetTemplate.BuildHtml(resetToken, resetUrl, expiresAt);
        var plain = PasswordResetTemplate.BuildPlainText(resetToken, resetUrl, expiresAt);

        return SendAsync(toEmail, PasswordResetTemplate.Subject, html, plain, attachment: null, cancellationToken);
    }

    [Obsolete("Dùng SendFirstLoginInvitationAsync cho tài khoản mới. Xem plan §2 N8.")]
    public Task SendAccountCredentialsAsync(
        string toEmail,
        string fullName,
        string username,
        string plainPassword,
        CancellationToken cancellationToken = default)
    {
        var html = AccountCredentialsTemplate.BuildHtml(fullName, username, plainPassword, _options.LoginUrl);
        var plain = AccountCredentialsTemplate.BuildPlainText(fullName, username, plainPassword, _options.LoginUrl);

        return SendAsync(toEmail, AccountCredentialsTemplate.Subject, html, plain, attachment: null, cancellationToken);
    }

    public Task SendFirstLoginInvitationAsync(
        string toEmail,
        string fullName,
        string username,
        string invitationUrl,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        var html = FirstLoginInvitationTemplate.BuildHtml(fullName, username, invitationUrl, expiresAt);
        var plain = FirstLoginInvitationTemplate.BuildPlainText(fullName, username, invitationUrl, expiresAt);

        return SendAsync(toEmail, FirstLoginInvitationTemplate.Subject, html, plain, attachment: null, cancellationToken);
    }

    public Task SendGenericNotificationAsync(
        string toEmail,
        string subject,
        string textBody,
        string? htmlBody,
        CancellationToken cancellationToken = default) =>
        SendAsync(toEmail, subject, htmlBody, textBody, attachment: null, cancellationToken);

    public async Task SendContractActivationAsync(
        string toEmail,
        string fullName,
        ContractActivationEmailPayload payload,
        string storageBucket,
        string storageObjectKey,
        string pdfFileName,
        CancellationToken cancellationToken = default)
    {
        var html = ContractActivationTemplate.BuildHtml(
            fullName, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo);
        var plain = ContractActivationTemplate.BuildPlainText(
            fullName, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo);

        // Tải PDF từ MinIO ngay lúc gửi (thay vì nhận stream). Giữ trong request/worker thread ngắn gọn.
        var download = await _storage.DownloadAsync(storageObjectKey, storageBucket, cancellationToken);
        await using var pdfStream = download.Content;

        var attachment = new EmailAttachment(pdfFileName, pdfStream, "application/pdf");
        await SendAsync(toEmail, ContractActivationTemplate.Subject, html, plain, attachment, cancellationToken);
    }

    private async Task SendAsync(
        string toEmail,
        string subject,
        string? htmlBody,
        string plainBody,
        EmailAttachment? attachment,
        CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogError(
                "SMTP is not configured (missing Host/Username/Password/FromEmail). Email to {Email} was NOT sent.",
                toEmail);
            throw new InvalidOperationException("SMTP configuration is incomplete.");
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { TextBody = plainBody };
        if (!string.IsNullOrWhiteSpace(htmlBody))
            builder.HtmlBody = htmlBody;

        if (attachment is not null)
            builder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));

        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            var secureSocket = _options.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_options.Host, _options.Port, secureSocket, cancellationToken);
            await client.AuthenticateAsync(_options.Username, _options.Password, cancellationToken);
            await client.SendAsync(message, cancellationToken);

            _logger.LogInformation("SMTP email sent to {Email} subject={Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMTP send failed for {Email} subject={Subject}", toEmail, subject);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, cancellationToken);
        }
    }

    private sealed record EmailAttachment(string FileName, Stream Content, string ContentType);
}
