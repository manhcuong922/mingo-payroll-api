using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.Contract.Queries;

public sealed record GetContractsQuery(
    int? EmployeeId,
    ContractStatus? Status,
    ContractCategory? Category,
    int Page,
    int Size) : IRequest<PagedResult<ContractSummaryDto>>;
