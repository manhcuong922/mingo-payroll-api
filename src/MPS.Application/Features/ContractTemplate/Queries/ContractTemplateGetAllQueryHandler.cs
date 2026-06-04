using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Queries;

public class ContractTemplateGetAllQueryHandler : IRequestHandler<ContractTemplateGetAllQuery, IReadOnlyList<ContractTemplateDto>>
{
    private readonly IContractTemplateRepository _repository;
    private readonly IMapper _mapper;

    public ContractTemplateGetAllQueryHandler(IContractTemplateRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ContractTemplateDto>> Handle(ContractTemplateGetAllQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.GetAllAsync(request.Category, request.Subtype, request.IsActive, cancellationToken);
        return _mapper.Map<IReadOnlyList<ContractTemplateDto>>(entities);
    }
}
