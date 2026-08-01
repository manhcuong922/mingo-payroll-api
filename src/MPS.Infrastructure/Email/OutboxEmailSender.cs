using System.Text.Json;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;

namespace MPS.Infrastructure.Email;

/// <summary>
/// Bản cài <see cref="IEmailSender"/> mà handler inject: KHÔNG gửi email, chỉ ghi một dòng
/// <see cref="EmailOutboxMessage"/> (Pending) vào DB. Worker <c>EmailDispatchJob</c> mới gửi thật.
/// </summary>
public sealed class OutboxEmailSender : IEmailSender
{
    private readonly IEmailOutboxRepository _outbox;
    private readonly IClock _clock;
    private readonly IOutboxPayloadProtector _protector;

    public OutboxEmailSender(IEmailOutboxRepository outbox, IClock clock, IOutboxPayloadProtector protector)
    {
        _outbox = outbox;
        _clock = clock;
        _protector = protector;
    }

    public Task SendPasswordResetAsync(
        string toEmail, string resetToken, DateTime expiresAt, CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.PasswordReset, toEmail,
            new PasswordResetPayload(resetToken, expiresAt),
            cancellationToken: cancellationToken);

    [Obsolete("Dùng SendFirstLoginInvitationAsync cho tài khoản mới. Xem plan §2 N8.")]
    public Task SendAccountCredentialsAsync(
        string toEmail, string fullName, string username, string plainPassword,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.AccountCredentials, toEmail,
            new AccountCredentialsPayload(fullName, username, plainPassword),
            cancellationToken: cancellationToken);

    public Task SendFirstLoginInvitationAsync(
        string toEmail, string fullName, string username, string invitationUrl, DateTime expiresAt,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.FirstLoginInvitation, toEmail,
            new FirstLoginInvitationPayload(fullName, username, invitationUrl, expiresAt),
            cancellationToken: cancellationToken);

    public Task SendContractActivationAsync(
        string toEmail, string fullName, ContractActivationEmailPayload payload,
        string storageBucket, string storageObjectKey, string pdfFileName,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.ContractActivation, toEmail,
            new ContractActivationPayload(
                fullName, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo, pdfFileName),
            storageBucket, storageObjectKey, cancellationToken);

    public Task SendGenericNotificationAsync(
        string toEmail, string subject, string textBody, string? htmlBody,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.GenericNotification, toEmail,
            new GenericNotificationPayload(subject, textBody, htmlBody),
            cancellationToken: cancellationToken);

    private Task EnqueueAsync<TPayload>(
        EmailOutboxType type, string toEmail, TPayload payload,
        string? storageBucket = null, string? storageObjectKey = null,
        CancellationToken cancellationToken = default)
    {
        var message = new EmailOutboxMessage
        {
            Type = type,
            ToEmail = toEmail,
            PayloadJson = _protector.Protect(JsonSerializer.Serialize(payload)),
            StorageBucket = storageBucket,
            StorageObjectKey = storageObjectKey,
            Status = EmailOutboxStatus.Pending,
            Attempts = 0,
            CreatedAt = _clock.UtcNow,
        };

        return _outbox.AddAsync(message, cancellationToken);
    }
}
