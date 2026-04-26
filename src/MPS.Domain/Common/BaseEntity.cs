namespace MPS.Domain.Common;

/// <summary>
/// Ví dụ Base Entity - lớp cơ sở cho tất cả Entity.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
