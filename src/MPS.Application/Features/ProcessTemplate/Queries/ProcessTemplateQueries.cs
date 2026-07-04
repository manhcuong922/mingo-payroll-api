using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;

namespace MPS.Application.Features.ProcessTemplate.Queries;

public record ProcessTemplateGetAllQuery : IRequest<IReadOnlyList<ProcessTemplateDto>>;

public record ProcessTemplateGetByIdQuery(int Id) : IRequest<ProcessTemplateDto?>;

public record ProcessTemplateGetPagedQuery(string? Keyword, int Page, int PageSize)
    : IRequest<PagedResult<ProcessTemplateDto>>;
