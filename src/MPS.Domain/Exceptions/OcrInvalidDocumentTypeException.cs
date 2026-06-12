namespace MPS.Domain.Exceptions;

public sealed class OcrInvalidDocumentTypeException : Exception
{
    public OcrInvalidDocumentTypeException(string? docType)
        : base($"OCR_INVALID_DOCUMENT_TYPE: Expected Vietnamese CCCD/CMND, got '{docType}'.") { }
}
