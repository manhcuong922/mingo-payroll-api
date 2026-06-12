namespace MPS.Domain.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
