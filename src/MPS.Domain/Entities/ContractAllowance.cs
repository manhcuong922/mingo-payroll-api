namespace MPS.Domain.Entities;

public class ContractAllowance
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public int AllowanceTypeId { get; set; }
    public AllowanceType AllowanceType { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
