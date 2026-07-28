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

    public Task SendAccountCredentialsAsync(
        string toEmail, string fullName, string username, string plainPassword,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.AccountCredentials, toEmail,
            new AccountCredentialsPayload(fullName, username, plainPassword),
            cancellationToken: cancellationToken);

    public Task SendContractActivationAsync(
        string toEmail, string fullName, ContractActivationEmailPayload payload,
        string storageBucket, string storageObjectKey, string pdfFileName,
        CancellationToken cancellationToken = default) =>
        EnqueueAsync(
            EmailOutboxType.ContractActivation, toEmail,
            new ContractActivationPayload(
                fullName, payload.ContractNo, payload.EffectiveFrom, payload.EffectiveTo,
                payload.Username, payload.PlainPassword, payload.LoginUrl, pdfFileName),
            storageBucket, storageObjectKey, cancellationToken);

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
