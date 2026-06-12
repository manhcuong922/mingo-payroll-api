using MPS.Domain.Enums.Contract;

namespace MPS.API.Requests.ContractTemplate;

public sealed record UpdateContractTemplateRequest(
    string Name,
    ContractCategory ContractCategory,
    ContractSubtype ContractSubtype,
    bool IsActive);
