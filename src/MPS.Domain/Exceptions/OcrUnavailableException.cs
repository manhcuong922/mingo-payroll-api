namespace MPS.Domain.Exceptions;

public sealed class OcrUnavailableException : Exception
{
    public OcrUnavailableException(Exception inner)
        : base("OCR_UNAVAILABLE: The OCR service is currently unavailable.", inner) { }
}
