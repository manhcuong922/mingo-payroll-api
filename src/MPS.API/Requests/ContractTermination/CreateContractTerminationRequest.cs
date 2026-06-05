using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.API.Requests.ContractTermination;

public sealed class CreateContractTerminationRequest
{
    public int ReasonRefId { get; set; }
    public ContractEnums.TerminationParty TerminationParty { get; set; }
    public DateTime TerminationDate { get; set; }
    public DateTime NoticeSentAt { get; set; }
    public int? NoticeDaysAhead { get; set; }
    public bool NoNoticeRequired { get; set; }
    public int? WrittenNoticeFileId { get; set; }
    public string LegalBasis { get; set; } = string.Empty;
    public string? Notes { get; set; }
}
