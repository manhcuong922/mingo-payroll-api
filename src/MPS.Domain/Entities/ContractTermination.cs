using ContractEnums = MPS.Domain.Enums.Contract;
using MPS.Domain.Attributes;

namespace MPS.Domain.Entities;

[Auditable]
public class ContractTermination
{
    public int Id { get; set; }
    public int ContractId { get; set; }
    public Contract Contract { get; set; } = null!;
    public DateTime TerminationDate { get; set; }
    public int ReasonRefId { get; set; }
    public ContractTerminationReasonRef ReasonRef { get; set; } = null!;
    public ContractEnums.TerminationParty TerminationParty { get; set; }
    public string LegalBasis { get; set; } = string.Empty;
    public DateTime? NoticeSentAt { get; set; }
    public int? NoticeDaysAhead { get; set; }
    public bool NoNoticeRequired { get; set; }
    public int? WrittenNoticeFileId { get; set; }
    public ContractFile? WrittenNoticeFile { get; set; }
    public int DecidedByUserId { get; set; }
    public User DecidedByUser { get; set; } = null!;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
