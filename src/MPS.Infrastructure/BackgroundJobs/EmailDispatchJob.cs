using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MPS.Application.Common.Interfaces;
using MPS.Domain.Entities;
using MPS.Domain.Enums;
using MPS.Domain.Interfaces;
using MPS.Infrastructure.Email;

namespace MPS.Infrastructure.BackgroundJobs;

/// <summary>
/// Worker poll bảng <see cref="EmailOutboxMessage"/> mỗi vài giây, gửi các message Pending qua
/// <see cref="IRealEmailSender"/>. Thành công → Sent (xóa payload nhạy cảm); lỗi → tăng Attempts +
/// đặt backoff; hết số lần thử → Failed (≈ DLQ). Cùng khuôn với các job khác (SemaphoreSlim + RunOnceAsync).
/// </summary>
public sealed class EmailDispatchJob : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailDispatchOptions _options;
    private readonly ILogger<EmailDispatchJob> _logger;

    public EmailDispatchJob(
        IServiceScopeFactory scopeFactory,
        IOptions<EmailDispatchOptions> options,
        ILogger<EmailDispatchJob> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, _options.PollingIntervalSeconds));
        _logger.LogInformation("EmailDispatchJob started; interval={Interval}.", interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailDispatchJob tick failed; retry next interval.");
            }

            try { await Task.Delay(interval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    /// <summary>Xử lý một lô message chờ gửi. Trả về số message đã gửi thành công.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            _logger.LogDebug("EmailDispatchJob already running; skipping.");
            return 0;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IEmailOutboxRepository>();
            var sender = scope.ServiceProvider.GetRequiredService<IRealEmailSender>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var protector = scope.ServiceProvider.GetRequiredService<IOutboxPayloadProtector>();

            var now = clock.UtcNow;
            var batch = await repo.GetPendingBatchAsync(_options.BatchSize, now, cancellationToken);
            if (batch.Count == 0) return 0;

            var sent = 0;
            foreach (var message in batch)
            {
                var ok = await TryDispatchAsync(message, sender, protector, clock, cancellationToken);
                if (ok) sent++;
            }

            await repo.SaveChangesAsync(cancellationToken);

            if (sent > 0)
                _logger.LogInformation("EmailDispatchJob: sent {Sent}/{Total} outbox messages.", sent, batch.Count);
            return sent;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> TryDispatchAsync(
        EmailOutboxMessage message, IRealEmailSender sender, IOutboxPayloadProtector protector,
        IClock clock, CancellationToken cancellationToken)
    {
        try
        {
            await SendAsync(message, sender, protector, cancellationToken);

            message.Status = EmailOutboxStatus.Sent;
            message.SentAt = clock.UtcNow;
            message.LastError = null;
            message.NextAttemptAt = null;
            message.PayloadJson = string.Empty; // Xóa dữ liệu nhạy cảm (mật khẩu thô) sau khi gửi.
            return true;
        }
        catch (Exception ex)
        {
            message.Attempts++;
            message.LastError = Truncate(ex.Message, 2000);

            if (message.Attempts >= _options.MaxAttempts)
            {
                message.Status = EmailOutboxStatus.Failed;
                message.NextAttemptAt = null;
                _logger.LogError(ex,
                    "EmailDispatchJob: message {Id} ({Type}) FAILED after {Attempts} attempts; moved to Failed.",
                    message.Id, message.Type, message.Attempts);
            }
            else
            {
                var backoff = TimeSpan.FromSeconds(
                    _options.BaseBackoffSeconds * Math.Pow(2, message.Attempts - 1));
                message.NextAttemptAt = clock.UtcNow.Add(backoff);
                _logger.LogWarning(ex,
                    "EmailDispatchJob: message {Id} ({Type}) attempt {Attempts} failed; retry after {Backoff}.",
                    message.Id, message.Type, message.Attempts, backoff);
            }
            return false;
        }
    }

    private static Task SendAsync(
        EmailOutboxMessage message, IRealEmailSender sender, IOutboxPayloadProtector protector,
        CancellationToken cancellationToken) =>
        message.Type switch
        {
            EmailOutboxType.PasswordReset => SendPasswordResetAsync(message, sender, protector, cancellationToken),
            EmailOutboxType.AccountCredentials => SendAccountCredentialsAsync(message, sender, protector, cancellationToken),
            EmailOutboxType.ContractActivation => SendContractActivationAsync(message, sender, protector, cancellationToken),
            _ => throw new InvalidOperationException($"Unknown EmailOutboxType {message.Type} for message {message.Id}."),
        };

    private static Task SendPasswordResetAsync(
        EmailOutboxMessage message, IRealEmailSender sender, IOutboxPayloadProtector protector, CancellationToken cancellationToken)
    {
        var p = Deserialize<PasswordResetPayload>(message, protector);
        return sender.SendPasswordResetAsync(message.ToEmail, p.ResetToken, p.ExpiresAt, cancellationToken);
    }

    private static Task SendAccountCredentialsAsync(
        EmailOutboxMessage message, IRealEmailSender sender, IOutboxPayloadProtector protector, CancellationToken cancellationToken)
    {
        var p = Deserialize<AccountCredentialsPayload>(message, protector);
        return sender.SendAccountCredentialsAsync(message.ToEmail, p.FullName, p.Username, p.PlainPassword, cancellationToken);
    }

    private static Task SendContractActivationAsync(
        EmailOutboxMessage message, IRealEmailSender sender, IOutboxPayloadProtector protector, CancellationToken cancellationToken)
    {
        var p = Deserialize<ContractActivationPayload>(message, protector);
        var payload = new ContractActivationEmailPayload(
            p.ContractNo, p.EffectiveFrom, p.EffectiveTo, p.Username, p.PlainPassword, p.LoginUrl);
        return sender.SendContractActivationAsync(
            message.ToEmail, p.FullName, payload,
            message.StorageBucket ?? throw new InvalidOperationException($"Message {message.Id} missing StorageBucket."),
            message.StorageObjectKey ?? throw new InvalidOperationException($"Message {message.Id} missing StorageObjectKey."),
            p.PdfFileName, cancellationToken);
    }

    private static T Deserialize<T>(EmailOutboxMessage message, IOutboxPayloadProtector protector) =>
        JsonSerializer.Deserialize<T>(protector.Unprotect(message.PayloadJson))
        ?? throw new InvalidOperationException($"Failed to deserialize payload for message {message.Id}.");

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
