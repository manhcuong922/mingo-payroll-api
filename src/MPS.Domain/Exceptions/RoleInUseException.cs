namespace MPS.Domain.Exceptions;

public sealed class RoleInUseException : Exception
{
    public RoleInUseException(string message)
        : base(message) { }
}
