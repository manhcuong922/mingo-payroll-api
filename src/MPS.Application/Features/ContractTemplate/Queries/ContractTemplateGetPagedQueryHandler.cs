using AutoMapper;
using MediatR;
using MPS.Application.Common.Models;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Queries;

public class ContractTemplateGetPagedQueryHandler
    : IRequestHandler<ContractTemplateGetPagedQuery, PagedResult<ContractTemplateDto>>
{
    private readonly IContractTemplateRepository _repository;
    private readonly IMapper _mapper;

    public ContractTemplateGetPagedQueryHandler(IContractTemplateRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<PagedResult<ContractTemplateDto>> Handle(
        ContractTemplateGetPagedQuery request,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedAsync(
            request.Kind, request.Category, request.Subtype, request.IsActive, request.Page, request.PageSize, cancellationToken);

        var dtos = _mapper.Map<IReadOnlyList<ContractTemplateDto>>(items);
        return new PagedResult<ContractTemplateDto>(dtos, total, request.Page, request.PageSize);
    }
}
