namespace MPS.Domain.Entities;

/// <summary>
/// Ví dụ Entity - đại diện cho một đối tượng trong hệ thống.
/// </summary>
public class ExampleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
