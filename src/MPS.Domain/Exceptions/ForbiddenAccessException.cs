namespace MPS.Domain.Exceptions;

public sealed class ForbiddenAccessException : Exception
{
    // FE dùng ErrorCode để hiển thị toast/UX riêng theo case (vd TASK_NOT_ASSIGNED) thay vì
    // parse message tiếng Việt. Null = 403 chung, chỉ có field "error" trong response.
    public string? ErrorCode { get; }

    public ForbiddenAccessException(string message = "Access to the requested resource is forbidden.", string? errorCode = null)
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
