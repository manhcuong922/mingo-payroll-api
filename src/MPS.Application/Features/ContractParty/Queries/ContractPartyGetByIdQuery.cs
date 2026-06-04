using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractParty.Queries;

public sealed record ContractPartyGetByIdQuery(int Id) : IRequest<ContractPartyDto?>;
