namespace MPS.Domain.Exceptions;

public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "Access to the requested resource is forbidden.")
        : base(message) { }
}
