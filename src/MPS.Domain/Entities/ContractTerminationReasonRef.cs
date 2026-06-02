namespace MPS.Domain.Entities;

public class ContractTerminationReasonRef
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresWrittenNotice { get; set; }

    public ICollection<ContractTermination> ContractTerminations { get; set; } = new List<ContractTermination>();
}
