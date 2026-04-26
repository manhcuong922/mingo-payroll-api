namespace MPS.Shared.Models;

/// <summary>
/// Ví dụ Shared Model - các model dùng chung (API Response, Pagination...).
/// </summary>
public class ExampleApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
}
