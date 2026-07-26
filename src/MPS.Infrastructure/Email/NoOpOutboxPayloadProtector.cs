namespace MPS.Infrastructure.Email;

/// <summary>
/// Không mã hóa — dùng khi chưa cấu hình <c>EmailDispatch:EncryptionKey</c> (dev/local).
/// Payload nằm plaintext trong bảng; chấp nhận được với LoggingEmailSender.
/// </summary>
public sealed class NoOpOutboxPayloadProtector : IOutboxPayloadProtector
{
    public string Protect(string plaintext) => plaintext;
    public string Unprotect(string ciphertext) => ciphertext;
}
