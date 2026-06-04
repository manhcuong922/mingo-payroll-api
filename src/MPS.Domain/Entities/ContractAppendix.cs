using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.Domain.Entities;

public class ContractAppendix
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public string AppendixNo { get; set; } = string.Empty;
    public ContractEnums.AppendixType AppendixType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ChangedClausesJson { get; set; } = string.Empty;
    public string AppendixText { get; set; } = string.Empty;
    public DateTime? NoticeSentAt { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? SignedAt { get; set; }
    public ContractEnums.AppendixStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
}
