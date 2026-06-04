namespace MPS.API.Requests.ContractParty;

public sealed record CreateContractPartyRequest(
    string LegalName,
    string TaxCode,
    string Address,
    string RepresentativeFullName,
    string RepresentativeTitle);
