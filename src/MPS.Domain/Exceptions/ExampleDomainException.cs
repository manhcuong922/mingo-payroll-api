namespace MPS.Domain.Exceptions;

/// <summary>
/// Ví dụ Domain Exception - lỗi nghiệp vụ thuộc tầng Domain.
/// </summary>
public class ExampleDomainException : Exception
{
    public ExampleDomainException(string message) : base(message) { }
}
