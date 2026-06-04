using MediatR;

namespace MPS.Application.Features.ContractParty.Commands;

public sealed record ContractPartyCreateCommand(
    string LegalName,
    string TaxCode,
    string Address,
    string RepresentativeFullName,
    string RepresentativeTitle) : IRequest<int>;
