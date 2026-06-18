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

    Task SendContractActivationAsync(
        string toEmail,
        string fullName,
        ContractActivationEmailPayload payload,
        Stream pdfStream,
        string pdfFileName,
        CancellationToken cancellationToken = default);
}

public sealed record ContractActivationEmailPayload(
    string ContractNo,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string? Username,
    string? PlainPassword,
    string? LoginUrl);
