namespace MPS.Application.Common.Exceptions.Storage;

public sealed class FileNotFoundInStorageException : FileStorageException
{
    public FileNotFoundInStorageException(string bucket, string objectKey)
        : base($"File '{objectKey}' was not found in bucket '{bucket}'.")
    {
        Bucket = bucket;
        ObjectKey = objectKey;
    }

    public string Bucket { get; }
    public string ObjectKey { get; }
}
