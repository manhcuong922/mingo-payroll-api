using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Domain.Entities;

public class ContractTemplate
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContractEnums.ContractCategory ContractCategory { get; set; }
    public ContractEnums.ContractSubtype ContractSubtype { get; set; }
    public int? TemplateFileId { get; set; }
    public ContractFile? TemplateFile { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
