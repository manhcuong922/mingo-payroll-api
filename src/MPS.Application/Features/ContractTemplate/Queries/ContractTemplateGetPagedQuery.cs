using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Queries;

public sealed record ContractTemplateGetPagedQuery(
    ContractCategory? Category,
    ContractSubtype? Subtype,
    bool? IsActive,
    int Page,
    int PageSize) : IRequest<PagedResult<ContractTemplateDto>>;
