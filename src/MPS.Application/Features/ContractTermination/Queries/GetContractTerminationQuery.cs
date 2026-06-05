using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractTermination.Queries;

public sealed record GetContractTerminationQuery(int ContractId) : IRequest<ContractTerminationDto?>;
