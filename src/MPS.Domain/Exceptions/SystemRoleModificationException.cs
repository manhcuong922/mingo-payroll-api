namespace MPS.Domain.Exceptions;

public sealed class SystemRoleModificationException : Exception
{
    public SystemRoleModificationException(string message)
        : base(message) { }
}
