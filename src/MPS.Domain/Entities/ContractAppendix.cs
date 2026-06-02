namespace MPS.Domain.Entities;

public class ContractAppendix
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string AppendixNo { get; set; } = string.Empty;
    public string AppendixType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ChangedClausesJson { get; set; } = string.Empty;
    public string AppendixText { get; set; } = string.Empty;
    public DateTime? NoticeSentAt { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? SignedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
