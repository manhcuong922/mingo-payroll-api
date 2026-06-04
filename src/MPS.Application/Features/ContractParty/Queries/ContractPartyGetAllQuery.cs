using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractParty.Queries;

public sealed record ContractPartyGetAllQuery(bool ActiveOnly)
    : IRequest<IReadOnlyList<ContractPartyDto>>;
