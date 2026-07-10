using MPS.Domain.Exceptions;

namespace MPS.Application.Features.Evaluation.Common;

/// <summary>Giải mã RowVersion base64 (client gửi lại từ DTO) → byte[] cho optimistic lock.</summary>
public static class RowVersionCodec
{
    public static byte[] Decode(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
            throw new DomainValidationException("RowVersion là bắt buộc khi ghi.");
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw new DomainValidationException("RowVersion không hợp lệ.");
        }
    }
}
