using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractAppendix.Queries;

public sealed record GetContractAppendicesQuery(int ContractId) : IRequest<IReadOnlyList<ContractAppendixDto>>;
