using MediatR;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ContractTemplate.Queries;

public sealed record ContractTemplateGetByIdQuery(int Id) : IRequest<ContractTemplateDto?>;
