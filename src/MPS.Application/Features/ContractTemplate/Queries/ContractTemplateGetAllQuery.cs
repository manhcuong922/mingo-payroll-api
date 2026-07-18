using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Enums.Contract;

namespace MPS.Application.Features.ContractTemplate.Queries;

public sealed record ContractTemplateGetAllQuery(
    ContractKind? Kind,
    ContractCategory? Category,
    ContractSubtype? Subtype,
    bool? IsActive) : IRequest<IReadOnlyList<ContractTemplateDto>>;
