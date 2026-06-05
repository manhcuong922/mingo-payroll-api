using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.Contract.Queries;

public sealed record GetContractByIdQuery(int Id) : IRequest<ContractDetailDto?>;
