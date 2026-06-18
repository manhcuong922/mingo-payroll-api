namespace MPS.Application.Common.Interfaces;

/// <summary>
/// Cung cấp metadata mức HTTP (IP, RequestPath, CorrelationId) cho audit log mà
/// không buộc Infrastructure phụ thuộc AspNetCore. Implementation thực tế nằm ở
/// MPS.API và đọc từ <c>IHttpContextAccessor</c>.
/// </summary>
public interface IAuditContextProvider
{
    string? IpAddress { get; }
    string? RequestPath { get; }
    string? CorrelationId { get; }
}
