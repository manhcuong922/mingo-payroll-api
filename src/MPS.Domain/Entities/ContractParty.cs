namespace MPS.Domain.Entities;

public class ContractParty
{
    public int Id { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RepresentativeFullName { get; set; } = string.Empty;
    public string RepresentativeTitle { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
}
