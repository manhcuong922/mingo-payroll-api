namespace MPS.Domain.Enums;

/// <summary>
/// Vòng đời một dòng outbox: Pending → Sent (thành công) hoặc Failed (hết số lần thử, ≈ DLQ).
/// </summary>
public enum EmailOutboxStatus : byte
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
}
