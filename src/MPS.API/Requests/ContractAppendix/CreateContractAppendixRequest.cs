using ContractEnums = MPS.Domain.Enums.Contract;

namespace MPS.API.Requests.ContractAppendix;

public sealed class CreateContractAppendixRequest
{
    public string AppendixNo { get; set; } = string.Empty;
    public ContractEnums.AppendixType AppendixType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ChangedClausesJson { get; set; } = string.Empty;
    public string AppendixText { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
}
