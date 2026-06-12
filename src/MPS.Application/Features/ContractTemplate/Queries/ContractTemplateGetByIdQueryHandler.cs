using AutoMapper;
using MediatR;
using MPS.Application.DTOs;
using MPS.Domain.Interfaces;

namespace MPS.Application.Features.ContractTemplate.Queries;

public class ContractTemplateGetByIdQueryHandler : IRequestHandler<ContractTemplateGetByIdQuery, ContractTemplateDto?>
{
    private readonly IContractTemplateRepository _repository;
    private readonly IMapper _mapper;

    public ContractTemplateGetByIdQueryHandler(IContractTemplateRepository repository, IMapper mapper)
    {
        _repository = repository;
        _mapper = mapper;
    }

    public async Task<ContractTemplateDto?> Handle(ContractTemplateGetByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetWithFileAsync(request.Id, cancellationToken);
        return entity is null ? null : _mapper.Map<ContractTemplateDto>(entity);
    }
}
