namespace MPS.Domain.Exceptions;

public sealed class OcrFailedException : Exception
{
    public OcrFailedException(string apiMessage) : base($"OCR_FAILED: {apiMessage}") { }
}
