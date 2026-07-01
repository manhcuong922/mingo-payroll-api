namespace MPS.Infrastructure.Email;

/// <summary>
/// Bảo vệ <c>EmailOutboxMessage.PayloadJson</c> lúc nằm ở trạng thái Pending (chứa dữ liệu
/// nhạy cảm như mật khẩu thô). Có key → AES-GCM; không key (dev) → no-op (plaintext).
/// </summary>
public interface IOutboxPayloadProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
