namespace MPS.Domain.Entities;

public class ContractTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContractCategory { get; set; } = string.Empty;
    public string ContractSubtype { get; set; } = string.Empty;
    public string TemplateBody { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
