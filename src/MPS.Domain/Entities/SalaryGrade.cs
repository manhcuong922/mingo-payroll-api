namespace MPS.Domain.Entities;

public class SalaryGrade
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public int Level { get; set; }
    public decimal BaseSalary { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
