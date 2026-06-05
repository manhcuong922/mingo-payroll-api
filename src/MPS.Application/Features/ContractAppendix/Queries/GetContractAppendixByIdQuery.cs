using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractAppendix.Queries;

public sealed record GetContractAppendixByIdQuery(int Id) : IRequest<ContractAppendixDto?>;
