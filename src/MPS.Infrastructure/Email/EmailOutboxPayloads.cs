namespace MPS.Infrastructure.Email;

// Hợp đồng nội bộ giữa OutboxEmailSender (ghi) và EmailDispatchJob (đọc) — serialize vào
// EmailOutboxMessage.PayloadJson. Không rò ra ngoài Infrastructure.

internal sealed record PasswordResetPayload(string ResetToken, DateTime ExpiresAt);

internal sealed record AccountCredentialsPayload(string FullName, string Username, string PlainPassword);

internal sealed record ContractActivationPayload(
    string FullName,
    string ContractNo,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string? Username,
    string? PlainPassword,
    string? LoginUrl,
    string PdfFileName);
