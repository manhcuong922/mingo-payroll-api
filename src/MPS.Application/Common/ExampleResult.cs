namespace MPS.Application.Common;

/// <summary>
/// Ví dụ Result Pattern - đối tượng bọc kết quả trả về thay vì throw exception.
/// </summary>
public class ExampleResult<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private ExampleResult(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static ExampleResult<T> Success(T value) => new(true, value, null);
    public static ExampleResult<T> Failure(string error) => new(false, default, error);
}
