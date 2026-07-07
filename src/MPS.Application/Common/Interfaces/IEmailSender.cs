namespace MPS.Application.Common.Interfaces;

public interface IEmailSender
{
    Task SendPasswordResetAsync(string toEmail, string resetToken, DateTime expiresAt, CancellationToken cancellationToken = default);

    Task SendAccountCredentialsAsync(
        string toEmail,
        string fullName,
        string username,
        string plainPassword,
        CancellationToken cancellationToken = default);

    // PDF không truyền qua stream nữa (để message-friendly cho outbox). Chỉ truyền vị trí
    // trên MinIO; bên gửi thật (IRealEmailSender) sẽ tự tải file khi cần.
    Task SendContractActivationAsync(
        string toEmail,
        string fullName,
        ContractActivationEmailPayload payload,
        string storageBucket,
        string storageObjectKey,
        string pdfFileName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cùng hợp đồng với <see cref="IEmailSender"/> nhưng đại diện cho bên GỬI THẬT (SMTP/logging).
/// Handler inject <see cref="IEmailSender"/> (ghi outbox); worker inject <see cref="IRealEmailSender"/> (gửi đi).
/// </summary>
public interface IRealEmailSender : IEmailSender
{
}

public sealed record ContractActivationEmailPayload(
    string ContractNo,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string? Username,
    string? PlainPassword,
    string? LoginUrl);
