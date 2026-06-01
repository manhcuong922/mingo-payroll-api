namespace MPS.Application.Common.Exceptions.Storage;

public sealed class FileStorageUnavailableException : FileStorageException
{
    public FileStorageUnavailableException(string message, Exception? inner = null)
        : base(message, inner ?? new Exception(message)) { }
}
