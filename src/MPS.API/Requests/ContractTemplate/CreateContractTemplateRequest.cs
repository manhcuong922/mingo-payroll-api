using MPS.Domain.Enums.Contract;

namespace MPS.API.Requests.ContractTemplate;

public sealed record CreateContractTemplateRequest(
    string Name,
    ContractCategory ContractCategory,
    ContractSubtype ContractSubtype,
    string TemplateBody,
    bool IsActive = true);
