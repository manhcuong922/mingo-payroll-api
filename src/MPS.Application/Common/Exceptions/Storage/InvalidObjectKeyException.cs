namespace MPS.Application.Common.Exceptions.Storage;

public sealed class InvalidObjectKeyException : FileStorageException
{
    public InvalidObjectKeyException(string objectKey, string reason)
        : base($"Invalid object key '{objectKey}': {reason}")
    {
        ObjectKey = objectKey;
        Reason = reason;
    }

    public string ObjectKey { get; }
    public string Reason { get; }
}
