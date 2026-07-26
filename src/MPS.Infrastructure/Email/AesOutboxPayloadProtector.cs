using System.Security.Cryptography;
using System.Text;

namespace MPS.Infrastructure.Email;

/// <summary>
/// Mã hóa payload bằng AES-256-GCM (authenticated encryption). Định dạng đầu ra:
/// base64( nonce(12) || ciphertext || tag(16) ). Key nạp từ base64 32 byte trong config.
/// </summary>
public sealed class AesOutboxPayloadProtector : IOutboxPayloadProtector
{
    private const int NonceSize = 12; // AesGcm.NonceByteSizes.MaxSize
    private const int TagSize = 16;   // AesGcm.TagByteSizes.MaxSize
    private const int KeySize = 32;   // 256-bit

    private readonly byte[] _key;

    public AesOutboxPayloadProtector(string base64Key)
    {
        byte[] key;
        try
        {
            key = Convert.FromBase64String(base64Key);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("EmailDispatch:EncryptionKey must be valid base64.", ex);
        }

        if (key.Length != KeySize)
            throw new InvalidOperationException(
                $"EmailDispatch:EncryptionKey must decode to {KeySize} bytes (256-bit); got {key.Length}.");

        _key = key;
    }

    public string Protect(string plaintext)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Encrypt(nonce, plainBytes, cipher, tag);

        var output = new byte[NonceSize + cipher.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, output, NonceSize + cipher.Length, TagSize);
        return Convert.ToBase64String(output);
    }

    public string Unprotect(string ciphertext)
    {
        var data = Convert.FromBase64String(ciphertext);
        if (data.Length < NonceSize + TagSize)
            throw new InvalidOperationException("Ciphertext too short to be valid AES-GCM output.");

        var nonce = data.AsSpan(0, NonceSize);
        var tag = data.AsSpan(data.Length - TagSize, TagSize);
        var cipher = data.AsSpan(NonceSize, data.Length - NonceSize - TagSize);
        var plain = new byte[cipher.Length];

        using (var aes = new AesGcm(_key, TagSize))
            aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
